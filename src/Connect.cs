using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace CodeAtlas
{
	/// <summary>用于实现外接程序的对象。</summary>
	/// <seealso class='IDTExtensibility2' />
	public class Connect : IDTExtensibility2, IDTCommandTarget
	{

		private delegate void CommandCallback(object[] param = null);
		private class CommandObj
		{
			public CommandObj(string name, string displayName, CommandCallback callback, string key = null)
			{
				this.name = name;
				this.displayName = displayName;
				this.callback = callback;
				this.key = key;
			}
			public string name { get; set; }
			public string displayName
			{
				get
				{
					if (m_displayName == null)
						return name;
					else
						return m_displayName;
				}
				set
				{
					m_displayName = value;
				}
			}
			public CommandCallback callback { get; set; }
			public string key { get; set; }
			public Command command { get; set; }
			private string m_displayName;
		}

		private DTE2 m_applicationObject;
		private AddIn m_addInInstance;

		private CommandObj[] m_commandList, m_socketCommandList;
		private SocketThread m_socket;
		private object		 m_toolBarObj;
		private CommandBarControl m_menuBarObj;

		/// <summary>实现外接程序对象的构造函数。请将您的初始化代码置于此方法内。</summary>
		public Connect()
		{
		}

		/// <summary>实现 IDTExtensibility2 接口的 OnConnection 方法。接收正在加载外接程序的通知。</summary>
		/// <param term='application'>宿主应用程序的根对象。</param>
		/// <param term='connectMode'>描述外接程序的加载方式。</param>
		/// <param term='addInInst'>表示此外接程序的对象。</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			m_applicationObject = (DTE2)application;
			m_addInInstance = (AddIn)addInInst;            			

            if ((connectMode == ext_ConnectMode.ext_cm_Startup || connectMode == ext_ConnectMode.ext_cm_AfterStartup) && m_socket == null)
			{
                initializeCallback();

				object []contextGUIDS = new object[] { };
				Commands2 commands = (Commands2)m_applicationObject.Commands;
				string toolsMenuName = "Tools";

				//将此命令置于“工具”菜单上。
				//查找 MenuBar 命令栏，该命令栏是容纳所有主菜单项的顶级命令栏:
				Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)m_applicationObject.CommandBars)["MenuBar"];

				//在 MenuBar 命令栏上查找“工具”命令栏:
//  				CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
//  				CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

				m_menuBarObj = menuBarCommandBar.Controls.Add(MsoControlType.msoControlPopup, Type.Missing, Type.Missing, Type.Missing, true);
				m_menuBarObj.Caption = "Code Atlas";
				CommandBarPopup toolsPopup = (CommandBarPopup)m_menuBarObj;

				// 增加工具栏
				//m_toolBarObj = m_applicationObject.Commands.AddCommandBar("Code Atlas Tools", vsCommandBarType.vsCommandBarTypeToolbar);

				//如果希望添加多个由您的外接程序处理的命令，可以重复此 try/catch 块，
				//  只需确保更新 QueryStatus/Exec 方法，使其包含新的命令名。
				try
				{
					foreach (Command cmd in commands)
					{
						foreach (CommandObj cmdObj in m_commandList)
						{
							if (cmd.Name == "CodeAtlas.Connect."+cmdObj.name)
							{
								cmdObj.command = cmd;
							}
						}
					}

					int nCommand = m_commandList.Length;
                    for (int ithCmd = 0; ithCmd < nCommand; ++ithCmd)
                    {
						if (m_commandList[ithCmd].command == null)
						{
							m_commandList[ithCmd].command = commands.AddNamedCommand2(
								m_addInInstance,
								m_commandList[ithCmd].name,
								m_commandList[ithCmd].displayName,
								"Executes the command for CodeAtlas",
								false, Type.Missing, ref contextGUIDS,
								(int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled,
								(int)vsCommandStyle.vsCommandStyleText,
								vsCommandControlType.vsCommandControlTypeButton);
						}
						Command cmd = m_commandList[ithCmd].command;

                        //将对应于该命令的控件添加到“工具”菜单:
						cmd.AddControl(toolsPopup.CommandBar, ithCmd + 1);
						//cmd.AddControl(m_toolBarObj, ithCmd + 1);
						string key = m_commandList[ithCmd].key;
						if (key != null && key.Length > 0)
						{
							cmd.Bindings = key;
						}
                    }
				}
				catch(System.ArgumentException exception)
				{
					//如果出现此异常，原因很可能是由于具有该名称的命令
					//  已存在。如果确实如此，则无需重新创建此命令，并且
					//  可以放心忽略此异常。
				}
                m_socket = new SocketThread("127.0.0.1", 12346, "127.0.0.1", 12345, onSocketCallback);
                m_socket.run();
            }
		}

		/// <summary>实现 IDTExtensibility2 接口的 OnDisconnection 方法。接收正在卸载外接程序的通知。</summary>
		/// <param term='disconnectMode'>描述外接程序的卸载方式。</param>
		/// <param term='custom'>特定于宿主应用程序的参数数组。</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
			onDisconnect();
		}

		private void onDisconnect()
		{
			if (m_socket != null)
			{
				m_socket.release();
				m_socket = null;
			}
			
			if (m_toolBarObj != null)
			{
				m_applicationObject.Commands.RemoveCommandBar(m_toolBarObj);
				m_toolBarObj = null;
			}


			int nCommand = m_commandList.Length;
			for (int ithCmd = 0; ithCmd < nCommand; ++ithCmd)
			{
				CommandObj cmdObj = m_commandList[ithCmd];
				if (cmdObj.command != null)
				{
					//m_commandList[ithCmd].command.Bindings = "";
					m_commandList[ithCmd].command.Delete();
					m_commandList[ithCmd].command = null;
				}
			}

			if (m_menuBarObj != null)
			{
				m_menuBarObj.Delete();
				m_menuBarObj = null;
			}
		}

		/// <summary>实现 IDTExtensibility2 接口的 OnAddInsUpdate 方法。当外接程序集合已发生更改时接收通知。</summary>
		/// <param term='custom'>特定于宿主应用程序的参数数组。</param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}

		/// <summary>实现 IDTExtensibility2 接口的 OnStartupComplete 方法。接收宿主应用程序已完成加载的通知。</summary>
		/// <param term='custom'>特定于宿主应用程序的参数数组。</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		/// <summary>实现 IDTExtensibility2 接口的 OnBeginShutdown 方法。接收正在卸载宿主应用程序的通知。</summary>
		/// <param term='custom'>特定于宿主应用程序的参数数组。</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
			onDisconnect();
		}

        /// <summary>实现 IDTCommandTarget 接口的 QueryStatus 方法。此方法在更新该命令的可用性时调用</summary>
        /// <param term='commandName'>要确定其状态的命令的名称。</param>
        /// <param term='neededText'>该命令所需的文本。</param>
        /// <param term='status'>该命令在用户界面中的状态。</param>
        /// <param term='commandText'>neededText 参数所要求的文本。</param>
        /// <seealso class='Exec' />
        public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
        {
            if (neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
            {
                if (commandName.StartsWith("CodeAtlas.Connect"))
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
            }
        }

        /// <summary>实现 IDTCommandTarget 接口的 Exec 方法。此方法在调用该命令时调用。</summary>
        /// <param term='commandName'>要执行的命令的名称。</param>
        /// <param term='executeOption'>描述该命令应如何运行。</param>
        /// <param term='varIn'>从调用方传递到命令处理程序的参数。</param>
        /// <param term='varOut'>从命令处理程序传递到调用方的参数。</param>
        /// <param term='handled'>通知调用方此命令是否已被处理。</param>
        /// <seealso class='Exec' />
        public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
        {
            handled = false;
            if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
            {
                for (int ithCmd = 0; ithCmd < m_commandList.Length; ++ithCmd)
                {
                    if (commandName == "CodeAtlas.Connect." + m_commandList[ithCmd].name)
                    {
                        handled = true;
						m_commandList[ithCmd].callback();
                        return;
                    }
                }
            }
        }

		private void onSocketCallback(string name, object[]param)
		{
			for (int ithCmd = 0; ithCmd < m_socketCommandList.Length; ++ithCmd)
			{
				if (name == m_socketCommandList[ithCmd].name)
				{
					m_socketCommandList[ithCmd].callback(param);
				}
			}
		}

        void onTest1(object[] param)
        {
			JsonPacket pk = JsonPacket.fromJson("{\"f\":\"fun\", \"p\":null}");
			string pkStr = pk.toJson();
			pk = JsonPacket.fromJson(pkStr);

			//m_socket.remoteCall("fun", new object[]{1, "obj"});
        }

		void onStartAtlas(object[] param)
        {

			string addInPath = m_addInInstance.SatelliteDllPath;
			int lastIdx = addInPath.LastIndexOf("\\");
			string fullFolder = addInPath.Substring(0, lastIdx);
						
// 			string solutionFolder = m_applicationObject.Solution.FullName;
// 			lastIdx = solutionFolder.LastIndexOf("\\");
// 			if (lastIdx >= 0)
// 				solutionFolder = solutionFolder.Substring(0, lastIdx);
// 			solutionFolder = solutionFolder.Replace("\\","/");
			
			string curDir = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
			curDir = System.Environment.CurrentDirectory;
			curDir = System.IO.Directory.GetCurrentDirectory();

			System.Diagnostics.Process p = new System.Diagnostics.Process();
			p.StartInfo.FileName = "cmd.exe";
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardInput = true;
			//p.StartInfo.RedirectStandardOutput = true;
			//p.StartInfo.RedirectStandardError = true;
			p.StartInfo.CreateNoWindow= false;
			p.StartInfo.WorkingDirectory = fullFolder;
			p.Start();
			p.StandardInput.WriteLine("codeView.bat");
			p.StandardInput.Flush();
        }
		 
		void onOpenDatabase(object[] param)
		{
			string fullName = m_applicationObject.Solution.FullName;
			int lastIdx = fullName.LastIndexOf("\\");
			string fullFolder = fullName.Substring(0, lastIdx);
			m_socket.remoteCall("onOpenPath", new object[] { fullFolder });
		}

		void onAnalyzeDatabase(object[] param)
		{
			m_socket.remoteCall("onAnalyze");
		}

		void onShowInAtlas(object[] param)
		{

			Document doc = m_applicationObject.ActiveDocument;
			TextSelection ts = doc.Selection as TextSelection;
			int lineOffset = ts.AnchorPoint.LineCharOffset;
			int lineNum = ts.AnchorPoint.Line;
			
			ts.SelectLine();
			string lineText = ts.Text;
			ts.MoveToLineAndOffset(lineNum, lineOffset);

			Regex rx = new Regex(@"\b(?<word>\w+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			MatchCollection matches = rx.Matches(lineText);

			// Report on each match.
			string token = null;
			foreach (Match match in matches)
			{
				string word = match.Groups["word"].Value;
				int startIndex = match.Index;
				int endIndex = startIndex + word.Length - 1;
				int lineIndex = lineOffset - 1;
				if (startIndex <= lineIndex && endIndex+1 >= lineIndex)
				{
					token = word;
					break;
				}
			}

			if (token != null)
			{
				string docPath = doc.FullName;
				m_socket.remoteCall("showInAtlas", new object[]{token, "*", docPath, lineNum});
			}

//			TextDocument txtDoc = doc.Object("TextDocument") as TextDocument;
// 			ProjectItem projectItem = doc.ProjectItem;
// 			FileCodeModel fileCodeModel = projectItem.FileCodeModel;
// 
// 			if (true || fileCodeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp)
// 			{
// 				EnvDTE.TextSelection txtSelection = m_applicationObject.ActiveDocument.Selection as EnvDTE.TextSelection;
// 				CodeElement codeEmelemt = null;
// 				try
// 				{
// 					codeEmelemt = fileCodeModel.CodeElementFromPoint(txtSelection.TopPoint, vsCMElement.vsCMElementFunction);
// 					string codeName = codeEmelemt.Name;
// 				}
// 				catch { }
// 			}

		}

		void onFindCallers(object[] param)
		{
			m_socket.remoteCall("onFindCallers");
		}

		void onFindCallees(object[] param)
		{
			m_socket.remoteCall("onFindCallees");
		}

		void onFindMembers(object[] param)
		{
			m_socket.remoteCall("onFindMembers");
		}

		void onFindBases(object[] param)
		{
			m_socket.remoteCall("onFindBases");
		}

		void onFindUses(object[] param)
		{
			m_socket.remoteCall("onFindUses");
		}

		void onGoToRight(object[] param)
		{
			m_socket.remoteCall("goToRight");
		}

		void onGoToLeft(object[] param)
		{
			m_socket.remoteCall("goToLeft");
		}

		void onGoToUp(object[] param)
		{
			m_socket.remoteCall("goToUp");
		}

		void onGoToDown(object[] param)
		{
			m_socket.remoteCall("goToDown");
		}

		void onDeleteSelectedItems(object[] param)
		{
			m_socket.remoteCall("onDeleteSelectedItems");
		}

		void onDeleteOldestItems(object[] param)
		{
			m_socket.remoteCall("onClearOldestItem");
		}

		void onTest2(object[] param)
		{
		}

		void goToPage(object[] param)
		{
			if (param == null || param.Length != 3 || param[0].GetType() != typeof(string) || param[1].GetType() != typeof(int) || param[2].GetType() != typeof(int))
			{
				return;
			}
			m_applicationObject.ItemOperations.OpenFile((string)param[0]);
			TextSelection ts = m_applicationObject.ActiveDocument.Selection as TextSelection;
			if (ts != null)
			{
				ts.GotoLine((int)param[1]);
			}
		}

        private void initializeCallback()
		{
			m_commandList = new CommandObj[] {
				//new CommandObj("test1", "test1", onTest1),
				new CommandObj("StartAtlas", "Start Atlas", onStartAtlas),
				//new CommandObj("OpenDatabase", "Open Database", onOpenDatabase),
				new CommandObj("AnalyzeDatabase", "Analyze Database", onAnalyzeDatabase),
				new CommandObj("ShowInAtlas", "Show In Atlas", onShowInAtlas, "文本编辑器::shift+alt+g"),
				new CommandObj("FindCallers", "Find Callers", onFindCallers, "文本编辑器::shift+alt+c"),
				new CommandObj("FindCallees", "Find Callees", onFindCallees, "文本编辑器::shift+alt+v"),
				new CommandObj("FindMembers", "Find Members", onFindMembers, "文本编辑器::shift+alt+m"),
				new CommandObj("FindBases", "Find Bases", onFindBases, "文本编辑器::shift+alt+b"),
				new CommandObj("FindUses", "Find Uses", onFindUses, "文本编辑器::shift+alt+u"),
				new CommandObj("GoToRight", "Go To Right", onGoToRight, "文本编辑器::shift+alt+d"),
				new CommandObj("GoToLeft", "Go To Left", onGoToLeft, "文本编辑器::shift+alt+a"),
				new CommandObj("GoToUp", "Go To Up", onGoToUp, "文本编辑器::shift+alt+w"),
				new CommandObj("GoToDown", "Go To Down", onGoToDown, "文本编辑器::shift+alt+s"),
				new CommandObj("DeleteSelectedItems", "Delete Selected Items", onDeleteSelectedItems, "文本编辑器::shift+alt+del"),
				new CommandObj("DeleteOldestItems", "Delete Oldest Items", onDeleteOldestItems),

			};
			m_socketCommandList = new CommandObj[] {
				new CommandObj("onTest2", "onTest2", onTest2),
				new CommandObj("goToPage", "goToPage", goToPage),
			};
        }

	}
}