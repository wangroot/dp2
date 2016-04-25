﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

using DigitalPlatform;
using DigitalPlatform.CirculationClient;
using DigitalPlatform.Text;
using DigitalPlatform.MessageClient;
using System.Collections;
// using Microsoft.AspNet.SignalR.Client.Hubs;


namespace dp2Circulation
{
    public partial class ChatForm : MyForm
    {
        public ChatForm()
        {
            InitializeComponent();

            this.webBrowser1.Width = 300;
            this.panel_input.Width = 300;
        }

        private void IMForm_Load(object sender, EventArgs e)
        {
            if (this.MainForm != null)
            {
                MainForm.SetControlFont(this, this.MainForm.DefaultFont);
            }

            this.ClearHtml();

            if (this.MainForm != null && this.MainForm.MessageHub != null)
                this.MainForm.MessageHub.AddMessage += MessageHub_AddMessage;

            Task.Factory.StartNew(() => DoLoadMessage("<default>"));
        }

        void MessageHub_AddMessage(object sender, AddMessageEventArgs e)
        {
            this.BeginInvoke(new Action<AddMessageEventArgs>(AddMessage), e);
        }

        void AddMessage(AddMessageEventArgs e)
        {
            foreach (MessageRecord record in e.Records)
            {
                // creator 要替换为用户名
                this.AddMessageLine(
                    IsMe(record) ? "right" : "left",
                    string.IsNullOrEmpty(record.userName) ? record.creator : record.userName,
                    record.data);
            }
        }

        // 是否为自己发出的消息
        bool IsMe(MessageRecord record)
        {
            if (record.userName == this.MainForm.MessageHub.UserName)
                return true;
            if (string.IsNullOrEmpty(this.MainForm.MessageHub.UserName))
            {
                string strParameters = this.MainForm.MessageHub.Parameters;
                Hashtable table = StringUtil.ParseParameters(strParameters, ',', '=', "url");
                string strLibraryName = (string)table["libraryName"];
                string strLibraryUID = (string)table["libraryUID"];
                string strLibraryUserName = (string)table["libraryUserName"];

                string strText = strLibraryUserName + "@";
                if (string.IsNullOrEmpty(strLibraryName))
                    strText += strLibraryName;
                else
                    strText += strLibraryUID;

                if (record.creator == strText)
                    return true;
            }

            return false;
        }

        private void IMForm_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void IMForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _redoLoadMesssageCount = 100; // 让重试尽快结束

            if (this.MainForm != null && this.MainForm.MessageHub != null)
                this.MainForm.MessageHub.AddMessage -= MessageHub_AddMessage;

            //CloseConnection();

            //this._channelPool.BeforeLogin -= new BeforeLoginEventHandle(_channelPool_BeforeLogin);
        }

        // 登录到 IM 服务器
        void SignIn()
        {

        }



        public override void EnableControls(bool bEnable)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<bool>(EnableControls), bEnable);
                return;
            }

            this.textBox_input.Enabled = bEnable;
            this.button_send.Enabled = bEnable;

            // base.EnableControls(bEnable);
        }

        void AddInfoLine(string strContent)
        {
            string strText = "<div class='item'>"
+ "<div class='item_line'>"
+ " <div class='item_summary'>" + HttpUtility.HtmlEncode(strContent).Replace("\r\n", "<br/>") + "</div>"
+ "</div>"
+ " <div class='clear'></div>"
+ "</div>";
            AppendHtml(strText);
        }

        void AddMessageLine(string left, string strName, string strContent)
        {
            if (strName == null)
                strName = "";
            if (strContent == null)
                strContent = "";

            string strText = "<div class='item'>"
+ "<div class='item_line_" + left + "'>"
+ " <div class='item_prefix_text_" + left + "'>" + HttpUtility.HtmlEncode(strName).Replace("\r\n", "<br/>") + "</div>"
+ " <div class='item_summary_" + left + "'>" + HttpUtility.HtmlEncode(strContent).Replace("\r\n", "<br/>") + "</div>"
+ "</div>"
+ " <div class='clear'></div>"
+ "</div>";
            AppendHtml(strText);
        }

        void AddErrorLine(string strContent)
        {
            string strText = "<div class='item error'>"
+ "<div class='item_line'>"
+ " <div class='item_summary'>" + HttpUtility.HtmlEncode(strContent).Replace("\r\n", "<br/>") + "</div>"
+ "</div>"
+ " <div class='clear'></div>"
+ "</div>";
            AppendHtml(strText);
        }


        /// 清除已有的 HTML 显示
        public void ClearHtml()
        {
            string strCssUrl = Path.Combine(this.MainForm.DataDir, "message.css");
            string strLink = "<link href='" + strCssUrl + "' type='text/css' rel='stylesheet' />";
            string strJs = "";

            {
                HtmlDocument doc = webBrowser1.Document;

                if (doc == null)
                {
                    webBrowser1.Navigate("about:blank");
                    doc = webBrowser1.Document;
                }
                doc = doc.OpenNew(true);
            }

            Global.WriteHtml(this.webBrowser1,
                "<html><head>" + strLink + strJs + "</head><body>");
        }

        // parameters:
        //      strText 要显示的文字。如果为空，表示清除以前的显示，本次也不显示任何东西
        public void HtmlWaiting(WebBrowser webBrowser,
            string strText)
        {
            string[] ids = new[] { "waiting1", "waiting2" };
            foreach (string id in ids)
            {
                HtmlElement obj = this.webBrowser1.Document.GetElementById(id);
                if (obj != null)
                    obj.OuterHtml = "";
            }

            if (string.IsNullOrEmpty(strText) == false)
            {
                string strGifFileName = Path.Combine(this.MainForm.DataDir, "ajax-loader3.gif");
                AppendHtml("<h2 id='waiting1' align='center'><img src='" + strGifFileName + "' /></h2>"
                    + "<h2 id='waiting2' align='center'>" + HttpUtility.HtmlEncode(strText) + "</h2>");
            }
        }

        // delegate void Delegate_AppendHtml(string strText);
        /// <summary>
        /// 向 IE 控件中追加一段 HTML 内容
        /// </summary>
        /// <param name="strText">HTML 内容</param>
        public void AppendHtml(string strText)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(AppendHtml), strText);
                return;
            }

            Global.WriteHtml(this.webBrowser1,
                strText);

            // 因为HTML元素总是没有收尾，其他有些方法可能不奏效
            this.webBrowser1.Document.Window.ScrollTo(0,
                this.webBrowser1.Document.Body.ScrollRectangle.Height);
        }

        private void button_send_Click(object sender, EventArgs e)
        {
#if NO
            string strUserName = this.MainForm.GetCurrentUserName();
            HubProxy.Invoke("Send", strUserName, this.textBox_input.Text);
            textBox_input.Text = string.Empty;
            textBox_input.Focus();
#endif
            if (string.IsNullOrEmpty(this.textBox_input.Text))
            {
                MessageBox.Show(this, "尚未输入文字");
                return;
            }
            Task.Factory.StartNew(() => SendMessage("<default>", this.textBox_input.Text));
        }

        void SendMessage(string strGroupName, string strText)
        {
            this.EnableControls(false);

            List<MessageRecord> messages = new List<MessageRecord>();
            MessageRecord record = new MessageRecord();
            record.group = strGroupName;
            record.data = strText;
            messages.Add(record);

            SetMessageRequest param = new SetMessageRequest("create",
                "",
               messages);

            SetMessageResult result = this.MainForm.MessageHub.SetMessageAsync(param).Result;
            if (result.Value == -1)
            {
                this.Invoke((Action)(() => MessageBox.Show(this, result.ErrorInfo)));
            }
            else
            {
                // 调用成功后才把输入的文字清除
                this.Invoke((Action)(() => this.textBox_input.Text = ""
                    ));
            }

            this.EnableControls(true);
        }

        int _redoLoadMesssageCount = 0;

        // 装载已经存在的消息记录
        async void DoLoadMessage(string strGroupName)
        {
            string strError = "";

            if (this.MainForm == null)
                return;

            // TODO: 如果当前 Connection 尚未连接，则要促使它连接，然后重试 load
            if (this.MainForm.MessageHub.IsConnected == false)
            {
                if (_redoLoadMesssageCount < 5)
                {
                    AddErrorLine("当前点对点连接尚未建立。重试操作中 ...");
                    this.MainForm.MessageHub.Connect();
                    Thread.Sleep(5000);
                    _redoLoadMesssageCount++;
                    Task.Factory.StartNew(() => DoLoadMessage(strGroupName));
                    return;
                }
                else
                {
                    AddErrorLine("当前点对点连接尚未建立。停止重试。消息装载失败。");
                    _redoLoadMesssageCount = 0; // 以后再调用本函数，就重新计算重试次数
                    return;
                }
            }

            this.Invoke((Action)(() => this.ClearHtml()));
            this.Invoke((Action)(() => this.HtmlWaiting(this.webBrowser1, "正在获取消息，请等待 ...")));

            EnableControls(false);
            try
            {
                CancellationToken cancel_token = new CancellationToken();

                string id = Guid.NewGuid().ToString();
                GetMessageRequest request = new GetMessageRequest(id,
                    strGroupName, // "" 表示默认群组
                    "",
                    "",
                    0,
                    -1);
                try
                {
                    MessageResult result = await this.MainForm.MessageHub.GetMessageAsync(
                        request,
                        FillMessage,
                        new TimeSpan(0, 1, 0),
                        cancel_token);
#if NO
                    this.Invoke(new Action(() =>
                    {
                        SetTextString(this.webBrowser1, ToString(result));
                    }));
#endif
                    if (result.Value == -1)
                    {
                        //strError = result.ErrorInfo;
                        //goto ERROR1;
                        this.AddErrorLine(result.ErrorInfo);
                    }
                }
                catch (AggregateException ex)
                {
                    strError = MessageConnection.GetExceptionText(ex);
                    goto ERROR1;
                }
                catch (Exception ex)
                {
                    strError = ex.Message;
                    goto ERROR1;
                }
                return;
            }
            finally
            {
                EnableControls(true);
                this.Invoke((Action)(() => this.HtmlWaiting(this.webBrowser1, "")));
            }
        ERROR1:
            this.Invoke((Action)(() => MessageBox.Show(this, strError)));
        }

        // 回调函数，用消息填充浏览器控件
        void FillMessage(long totalCount,
    long start,
    IList<MessageRecord> records,
    string errorInfo,
    string errorCode)
        {
            if (this.webBrowser1.InvokeRequired)
            {
                this.webBrowser1.Invoke(new Action<long, long, IList<MessageRecord>, string, string>(FillMessage),
                    totalCount, start, records, errorInfo, errorCode);
                return;
            }

            if (records != null)
            {
                foreach (MessageRecord record in records)
                {
                    // creator 要替换为用户名
                    this.AddMessageLine(
                        IsMe(record) ? "right" : "left",
                        string.IsNullOrEmpty(record.userName) ? record.creator : record.userName,
                        record.data);
                }
            }
        }

        // 书目检索
        private void toolStripButton_searchBiblio_Click(object sender, EventArgs e)
        {
#if NO
            Task<MessageResult> task = HubProxy.Invoke<MessageResult>("RequestSearchBiblio",
                "<全部>",
                "中国",
                "<全部>",
                "left",
                "",
(Int64)100);

            while (task.IsCompleted == false)
            {
                Application.DoEvents();
                Thread.Sleep(200);
            }

            if (task.IsFaulted == true)
            {
                AddErrorLine(GetExceptionText(task.Exception));
                return;
            }

            MessageResult result = task.Result;
            if (result.Value == -1)
            {
                AddErrorLine(result.ErrorInfo);
                return;
            }
            if (result.Value == 0)
            {
                AddErrorLine(result.ErrorInfo);
                return;
            }
            AddMessageLine("search ID:", result.String);

            // 出现对话框

            // DoSearchBiblio();
#endif
        }

#if NO
        public async void DoSearchBiblio()
        {
            MessageResult result = await HubProxy.Invoke<MessageResult>(
                "RequestSearchBiblio",
                "<全部>",
                "中国",
                "<全部>",
                "left",
                100);
            if (result.Value == -1)
            {
                AddErrorLine(result.ErrorInfo);
                return;
            }

            string strSearchID = result.String;
            AddMessageLine("search ID:", result.String);
        }  
#endif
    }


}
