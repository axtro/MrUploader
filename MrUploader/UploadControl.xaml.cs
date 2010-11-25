using System;
using System.Linq;
using System.Net;
using System.Net.Browser;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Browser;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MrUploader
{
	public partial class FileUploadControl : UserControl
	{
		private ObservableCollection<FileUpload> files;
		private DispatcherTimer notifyTimer;
		private ScriptObject callback;
		private int notifyJsTick;

		public string CallbackData { get; set; }
		public string FileDialogFilter { get; set; }

		public FileUploadControl()
		{
			files = new ObservableCollection<FileUpload>();
			InitializeComponent();
			Loaded += new RoutedEventHandler(Page_Loaded);
			notifyTimer = new DispatcherTimer();
			FileDialogFilter = "All Files|*.*";
			/*
			 We could use the following line to switch to ClientHttp http stack.
			 In this case we will have one advantage: correct response code from server (to distinguish 201 and 200 code).
			 But, we will have big disadvantages:
				1. Lack of proxy authorization
				2. Necessity to add cookies to request manually
			*/
			//bool httpResult = WebRequest.RegisterPrefix("http://", WebRequestCreator.ClientHttp);
		}
		public void StartNotifyJS()
		{
			notifyJsTick = Constants.JsNotifyTryies;
			notifyTimer.Interval = new TimeSpan(0, 0, 0, 0, Constants.JsNotifyInterval);
			notifyTimer.Tick += new EventHandler(NotifyJS);
			notifyTimer.Start();
		}
		public void NotifyJS(object o, EventArgs sender)
		{
			bool wasError = false;
			notifyJsTick--;
			try
			{
				//HtmlPage.Window.Invoke("UploadPluginReady", "Silverlight", Environment.Version.ToString(), 0);
			}
			catch (Exception)
			{
				wasError = true;
			}
			if (wasError == false || notifyJsTick <= 0)
			{
				notifyTimer.Stop();
			}
		}
		void Page_Loaded(object sender, RoutedEventArgs e)			
		{
			HtmlPage.RegisterScriptableObject("API", this);
			StartNotifyJS();
		}

		[ScriptableMember]
		public void registerCallback(ScriptObject sender)
		{
			callback = sender;
		}

		void addFilesButton_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.Filter = FileDialogFilter;
			dlg.Multiselect = false;

			if ((bool)dlg.ShowDialog())
			{
				files.Clear();
				foreach (FileInfo file in dlg.Files)
				{
					FileUpload upload = new FileUpload(this.Dispatcher, file);

					upload.StatusChanged += new EventHandler(upload_StatusChanged);
					upload.UploadProgressChanged += new ProgressChangedEvent(upload_UploadProgressChanged);
					//debug
					upload.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(upload_Debug);
					try
					{
						callback.Invoke("onFileChoose", upload.SessionId, upload.Name, upload.FileLength);
						//HtmlPage.Window.Invoke("newUploadCallback", CallbackData, upload.SessionId, upload.Name, upload.FileLength, DateTime.Now.Millisecond, dlg.Files.Count());
					}
					catch (Exception) { }
					files.Add(upload);
				}
			}
		}

		void upload_Debug(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			FileUpload fu = sender as FileUpload;
			try
			{
				HtmlPage.Window.Invoke("alert", "Debug: " + fu.DebugText);
			}
			catch (Exception) { }
		}

		void upload_UploadProgressChanged(object sender, UploadProgressChangedEventArgs args)
		{
			FileUpload fu = sender as FileUpload;
			try
			{
				callback.Invoke("onUploadProgress", fu.SessionId, args.TotalBytesUploaded, args.TotalBytes);
				//HtmlPage.Window.Invoke("UploadProgressCallback", CallbackData, fu.SessionId, args.TotalBytesUploaded, args.TotalBytes);
			}
			catch (Exception) {}
		}

		void upload_StatusChanged(object sender, EventArgs e)
		{
			FileUpload fu = sender as FileUpload;
			if (fu.Status == FileUploadStatus.Complete)
			{
				try
				{
					callback.Invoke("onUploadSuccess", fu.SessionId);
					//HtmlPage.Window.Invoke("UploadDataCallback", CallbackData, fu.SessionId, fu.ResponseText, fu.FileLength);
				}
				catch (Exception) {}
			}
			else if (fu.Status == FileUploadStatus.Failed)
			{
				try
				{
					callback.Invoke("onUploadFailed", fu.SessionId);
					//HtmlPage.Window.Invoke("UploadFailedCallback", CallbackData, fu.SessionId, fu.ErrorCode, fu.ErrorDescr);
				}
				catch (Exception) {}
			}
			else if (fu.Status == FileUploadStatus.Retry)
			{
				fu.RetryUpload();
			}
			else if (fu.Status == FileUploadStatus.Continue)
			{
				fu.UploadFileEx();
			}
			else if (fu.Status == FileUploadStatus.Canceled)
			{
				try
				{
					callback.Invoke("onUploadCanceled", fu.SessionId);
					//HtmlPage.Window.Invoke("UploadFailedCallback", CallbackData, fu.SessionId, fu.ErrorCode, fu.ErrorDescr);
				}
				catch (Exception) { }
			}
		 }

		[ScriptableMember]
		public void startUpload(string url, string additionalData)
		{
			FileUpload fu = files.First();
			try
			{
				if (fu.UploadUrl == null || fu.UploadUrl.ToString().Length == 0) // it can be set from IsoStorage
				{
					if (additionalData.Length != 0)
					{
						fu.UploadUrl = new Uri(url + "?" + additionalData);
					}
					else
					{
						fu.UploadUrl = new Uri(url);
					}
				}
			}
			catch (Exception) { }
			fu.StartUpload();
		}

		[ScriptableMember]
		public void cancelUpload()
		{
			FileUpload fu = files.First();
			fu.CancelUpload();
		}

	}
}
