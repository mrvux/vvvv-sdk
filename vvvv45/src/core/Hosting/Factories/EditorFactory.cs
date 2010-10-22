﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;

using VVVV.Core;
using VVVV.Core.Logging;
using VVVV.Core.Model;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;

namespace VVVV.Hosting.Factories
{
	[Export(typeof(IAddonFactory))]
	public class EditorFactory : IAddonFactory, IMouseClickListener
	{
		[ImportMany(typeof(IEditor))]
		protected List<ExportFactory<IEditor, IEditorInfo>> FNodeInfoExports;
		
		[Import]
		protected IHDEHost FHDEHost;
		
		[Import]
		protected ISolution FSolution;
		
		ILogger FLogger;
		CompositionContainer FContainer;
		HostExportProvider FHostExportProvider;
		ExportProvider[] FExportProviders;
		Dictionary<INodeInfo, ExportFactory<IEditor, IEditorInfo>> FNodeInfos;
		Dictionary<IPluginHost2, ExportLifetimeContext<IEditor>> FExportLifetimeContexts;
		
		[ImportingConstructor]
		public EditorFactory(CompositionContainer parentContainer, ILogger logger)
		{
			FHostExportProvider = new HostExportProvider();
			FExportProviders = new ExportProvider[] { parentContainer, FHostExportProvider };
			FNodeInfos = new Dictionary<INodeInfo, ExportFactory<IEditor, IEditorInfo>>();
			FExportLifetimeContexts = new Dictionary<IPluginHost2, ExportLifetimeContext<IEditor>>();
			FLogger = logger;
		}
		
		public IEnumerable<INodeInfo> ExtractNodeInfos(string filename)
		{
			// Present the user with all files associated with this filename.
			var nodeInfo = CreateNodeInfo(filename);
			if (nodeInfo != null)
				yield return nodeInfo;
			
			// Do we have a project file?
			var project = FSolution.FindProject(filename);
			if (project != null)
			{
				if (!project.IsLoaded)
					project.Load();
				
				foreach (var doc in project.Documents)
				{
					var docFilename = doc.Location.LocalPath;
					
					if (docFilename != filename)
					{
						nodeInfo = CreateNodeInfo(doc.Location.LocalPath);
						if (nodeInfo != null)
							yield return nodeInfo;
					}
				}
			}
		}
		
		private INodeInfo CreateNodeInfo(string filename)
		{
			var fileExtension = Path.GetExtension(filename);
			
			foreach (var nodeInfoExport in FNodeInfoExports)
			{
				if (nodeInfoExport.Metadata.FileExtensions.Contains(fileExtension))
				{
					var nodeInfo = new NodeInfo();
					nodeInfo.Name = Path.GetFileName(filename);
					nodeInfo.Type = NodeType.Text;
					nodeInfo.Filename = filename;
					nodeInfo.Ignore = true;
					nodeInfo.InitialBoxSize = new System.Drawing.Size(200, 100);
					nodeInfo.InitialWindowSize = new System.Drawing.Size(700, 800);
					nodeInfo.InitialComponentMode = TComponentMode.InAWindow;
					
					FNodeInfos[nodeInfo] = nodeInfoExport;
					
					return nodeInfo;
				}
			}
			
			return null;
		}
		
		public bool Create(INodeInfo nodeInfo, IAddonHost host)
		{
			var editorHost = host as IPluginHost2;
			
			if (editorHost != null && FNodeInfos.ContainsKey(nodeInfo))
			{
				var nodeInfoExport = FNodeInfos[nodeInfo];
				var exportLifetimeContext = nodeInfoExport.CreateExport();
				FExportLifetimeContexts[editorHost] = exportLifetimeContext;
				
				var editor = exportLifetimeContext.Value;
				editorHost.Plugin = editor;
				
				editor.Open(nodeInfo.Filename);
				
				return true;
			}
			
			return false;
		}
		
		public bool Delete(INodeInfo nodeInfo, IAddonHost host)
		{
			var editorHost = host as IPluginHost2;
			
			if (editorHost != null && FNodeInfos.ContainsKey(nodeInfo))
			{
				var exportLifetimeContext = FExportLifetimeContexts[editorHost];
				exportLifetimeContext.Dispose();
				FExportLifetimeContexts.Remove(editorHost);
			}
			
			return false;
		}
		
		public bool Clone(INodeInfo nodeInfo, string path, string name, string category, string version)
		{
			// TODO: What to do here?
			return false;
		}
		
		public event NodeInfoEventHandler NodeInfoAdded;
		
		protected virtual void OnNodeInfoAdded(INodeInfo nodeInfo)
		{
			if (NodeInfoAdded != null)
				NodeInfoAdded(this, nodeInfo);
		}
		
		public event NodeInfoEventHandler NodeInfoRemoved;
		
		protected virtual void OnNodeInfoRemoved(INodeInfo nodeInfo)
		{
			if (NodeInfoRemoved != null)
				NodeInfoRemoved(this, nodeInfo);
		}
		
		public string JobStdSubPath {
			get {
				return "editors";
			}
		}
		
		public void AddDir(string dir)
		{
			try
			{
				var catalog = new DirectoryCatalog(Shell.CallerPath.ConcatPath(@"..\..\editors"));
				FContainer = new CompositionContainer(catalog, FExportProviders);
				FContainer.ComposeParts(this);
			}
			catch (ReflectionTypeLoadException e)
			{
				foreach (var f in e.LoaderExceptions)
					FLogger.Log(f);
				return;
			}
			catch (Exception e)
			{
				FLogger.Log(e);
				return;
			}
		}
		
		public void RemoveDir(string dir)
		{
			throw new NotImplementedException();
		}

		public event NodeInfoEventHandler NodeInfoUpdated;

		public void MouseDownCB(INode node, Mouse_Buttons button, Modifier_Keys keys)
		{
			if ((node != null) && (button == Mouse_Buttons.Left) && (keys == Modifier_Keys.Control))
			{
				var nodeInfo = node.GetNodeInfo();
				FHDEHost.Open(nodeInfo.Filename, false);
			}
		}
		
		public void MouseUpCB(INode node, Mouse_Buttons button, Modifier_Keys keys)
		{
		}
	}
	
	public interface IEditorInfo
	{
		string[] FileExtensions { get; }
	}
}
