﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;


using DigitalPlatform.EasyMarc;
using DigitalPlatform.CommonControl;
using DigitalPlatform.Text;
using System.Diagnostics;
using DigitalPlatform.Script;
using DigitalPlatform;
using DigitalPlatform.CirculationClient.localhost;
using DigitalPlatform.Marc;
using System.Xml;
using DigitalPlatform.GUI;
using DigitalPlatform.Xml;

namespace dp2Circulation
{
    /// <summary>
    /// 处理一条书目记录和下属册记录的界面事务的包装类
    /// </summary>
    public class BiblioAndEntities
    {
        public Form Owner = null;
        public EasyMarcControl easyMarcControl1 = null;     // MarcControl
        public FlowLayoutPanel flowLayoutPanel1 = null;     // ItemsContainer

        string OldMARC = "";
        public byte [] Timestamp = null;

        public string ServerName = "";
        public string BiblioRecPath = "";

        string MarcSyntax = "";

        /// <summary>
        /// 获得值列表
        /// </summary>
        public event GetValueTableEventHandler GetValueTable = null;

        public event DeleteItemEventHandler DeleteItem = null;

        public event GetDefaultItemEventHandler GetDefaultItem = null;

        /// <summary>
        /// 通知需要装载下属的册记录
        /// 触发前，要求 BiblioRecPath 有当前书目记录的路径
        /// </summary>
        public event EventHandler LoadEntities = null;

        public BiblioAndEntities(
            Form owner,
            EasyMarcControl easyMarcControl1,
            FlowLayoutPanel flowLayoutPanel1)
        {
            this.Owner = owner;
            this.easyMarcControl1 = easyMarcControl1;
            this.flowLayoutPanel1 = flowLayoutPanel1;
        }

        /// <summary>
        /// 书目记录内容是否发生过修改
        /// </summary>
        public bool BiblioChanged
        {
            get
            {
                return this.easyMarcControl1.Changed;
            }
            set
            {
                if (this.easyMarcControl1.Changed != value)
                {
                    this.easyMarcControl1.Changed = value;
                }
            }
        }

        // 从列表中选择一条书目记录装入编辑模板
        // return:
        //      -1  出错
        //      0   放弃装入
        //      1   成功装入
        public int SetBiblio(
            RegisterBiblioInfo info,
            out string strError)
        {
            strError = "";

            if (this.BiblioChanged == true)
            {
                DialogResult result = MessageBox.Show(this.Owner,
"当前书目记录修改后尚未保存。如果此时装入新记录内容，先前的修改将会丢失。\r\n\r\n是否装入新记录?",
"BiblioRegisterWizard",
MessageBoxButtons.YesNo,
MessageBoxIcon.Question,
MessageBoxDefaultButton.Button2);
                if (result == DialogResult.No)
                    return 0;
            }

#if NO
            // 警告那些从原书目记录下属装入的册记录修改。但新增的册不会被清除
            if (HasEntitiesChanged("normal") == true)
            {
                DialogResult result = MessageBox.Show(this.Owner,
"当前有册记录修改后尚未保存。如果此时装入新记录内容，先前的修改将会丢失。\r\n\r\n是否装入新记录?",
"BiblioRegisterControl",
MessageBoxButtons.YesNo,
MessageBoxIcon.Question,
MessageBoxDefaultButton.Button2);
                if (result == DialogResult.No)
                    return 0;
            }
            
            this.ClearEntityEditControls("normal");

#endif
            if (this.EntitiesChanged == true)
            {
                DialogResult result = MessageBox.Show(this.Owner,
"当前有册记录修改后尚未保存。如果此时装入新记录内容，先前的修改将会丢失。\r\n\r\n是否装入新记录?",
"BiblioRegisterControl",
MessageBoxButtons.YesNo,
MessageBoxIcon.Question,
MessageBoxDefaultButton.Button2);
                if (result == DialogResult.No)
                    return 0;
            }
            this.ClearEntityEditControls();

            this.OldMARC = info.OldXml;
            this.Timestamp = info.Timestamp;

            string strPath = "";
            string strServerName = "";
            StringUtil.ParseTwoPart(info.RecPath, "@", out strPath, out strServerName);
            this.ServerName = strServerName;
            this.BiblioRecPath = strPath;

            this.MarcSyntax = info.MarcSyntax;

            this.easyMarcControl1.SetMarc(info.OldXml);
            Debug.Assert(this.BiblioChanged == false, "");

            // 设置封面图像
#if NO
            string strMARC = info.OldXml;
            if (string.IsNullOrEmpty(strMARC) == false)
            {
                this.ImageUrl = ScriptUtil.GetCoverImageUrl(strMARC);
                this.CoverImageRequested = false;
            }
#endif

            if (this.LoadEntities != null)
                this.LoadEntities(this, new EventArgs());

            return 1;
        ERROR1:
            // MessageBox.Show(this, strError);
            return -1;
        }

        // parameters:
        //      strStyle    清除哪些部分? all/normal
        //                  normal 表示只清除那些从数据库中调出的记录
        public void ClearEntityEditControls(string strStyle = "all")
        {
            if (this.easyMarcControl1.InvokeRequired)
            {
                // 事件是在多线程上下文中触发的，需要 Invoke 显示信息
                this.easyMarcControl1.Invoke(new Action<string>(ClearEntityEditControls), strStyle);
                return;
            }

            List<Control> controls = new List<Control>();
            foreach (Control control in this.flowLayoutPanel1.Controls)
            {
                if (control is EntityEditControl)
                {
                    EntityEditControl edit = control as EntityEditControl;

                    if (strStyle == "normal")
                    {
                        if (edit.CreateState != ItemDisplayState.Normal)
                            continue;
                    }

                    controls.Add(edit);
                    edit.PaintContent -= new PaintEventHandler(control_PaintContent);
                    edit.ContentChanged -= new DigitalPlatform.ContentChangedEventHandler(control_ContentChanged);
                }
                else
                    controls.Add(control);
            }

            if (strStyle != "normal")
                this.flowLayoutPanel1.Controls.Clear();
            else
            {
                foreach (Control control in controls)
                {
                    this.flowLayoutPanel1.Controls.Remove(control);
                }
            }

            InvalidateEditControls(-1);

            this.AdjustFlowLayoutHeight();
        }

        // 探测实体界面是否发生过修改?
        // parameters:
        //      strStyle    探测哪些部分? all/normal
        //                  normal 表示只清除那些从数据库中调出的记录
        bool HasEntitiesChanged(string strStyle = "all")
        {
            foreach (Control control in this.flowLayoutPanel1.Controls)
            {
                if (control is EntityEditControl)
                {
                    EntityEditControl edit = control as EntityEditControl;

                    if (strStyle == "normal")
                    {
                        if (edit.CreateState != ItemDisplayState.Normal)
                            continue;
                    }

                    if (edit.Changed == true)
                        return true;
                }
            }

            return false;
        }

        bool m_bEntitiesChanged = false;

        /// <summary>
        /// 册记录内容是否发生过修改
        /// </summary>
        public bool EntitiesChanged
        {
            get
            {
                return this.m_bEntitiesChanged;
            }
            set
            {
                if (this.m_bEntitiesChanged != value)
                {
                    this.m_bEntitiesChanged = value;
                }
            }
        }

        // 添加一个新的册对象
        // parameters:
        //      strRecPath  记录路径
        public int NewEntity(string strRecPath,
            byte[] timestamp,
            string strXml,
            bool ScrollIntoView,
            out string strError)
        {
            strError = "";

#if NO
            if (this.easyMarcControl1.InvokeRequired)
            {
                Delegate_NewEntity d = new Delegate_NewEntity(NewEntity);
                object[] args = new object[5];
                args[0] = strRecPath;
                args[1] = timestamp;
                args[2] = strXml;
                args[3] = ScrollIntoView;
                args[4] = strError;
                int result = (int)this.easyMarcControl1.Invoke(d, args);

                // 取出out参数值
                strError = (string)args[4];
                return result;
            }
#endif

            EntityEditControl control = new EntityEditControl();
            control.DisplayMode = "simple_register";
            control.Width = 120;
            control.AutoScroll = false;
            control.AutoSize = true;
            control.Font = this.Owner.Font;
            control.BackColor = Color.Transparent;
            control.Margin = new Padding(8, 8, 8, 8);

            // control.ErrorInfo = "测试文字 asdfasdf a asd fa daf a df af asdf asdf adf asdf asdf asf asdf asdf ---- ";

            if (string.IsNullOrEmpty(strXml) == false)
            {
                int nRet = control.SetData(strXml, strRecPath, timestamp, out strError);
                if (nRet == -1)
                    return -1;
            }
            else
            {
                control.Initializing = false;
                // control.Barcode = strItemBarcode;
                if (string.IsNullOrEmpty(control.RefID) == true)
                    control.RefID = Guid.NewGuid().ToString();
            }

            if (timestamp == null)
            {
                control.CreateState = ItemDisplayState.New;
                control.Changed = true;
                this.EntitiesChanged = true;    // 让外界能感知到含有新册事项
            }

            control.PaintContent += new PaintEventHandler(control_PaintContent);
            control.ContentChanged += new DigitalPlatform.ContentChangedEventHandler(control_ContentChanged);
            control.GetValueTable += new DigitalPlatform.GetValueTableEventHandler(control_GetValueTable);
            control.AppendMenu += new ApendMenuEventHandler(control_AppendMenu);

            // ClearBlank();

            control.BackColor = ControlPaint.Dark(this.flowLayoutPanel1.BackColor);
            // control.MemberBackColor = this.flowLayoutPanel1.ForeColor;
            control.MemberForeColor = ControlPaint.LightLight(this.flowLayoutPanel1.BackColor);
            control.SetAllEditColor(this.flowLayoutPanel1.BackColor, this.flowLayoutPanel1.ForeColor);
#if NO
            control.BackColor = this.flowLayoutPanel1.BackColor;
            control.ForeColor = this.flowLayoutPanel1.ForeColor;
#endif
            
            // this.flowLayoutPanel1.Controls.Add(control);
            AddEditControl(control);

            // this.flowLayoutPanel1.PerformLayout();
            // this.tableLayoutPanel1.PerformLayout();

            this.AdjustFlowLayoutHeight();

            if (ScrollIntoView)
            {
                this.flowLayoutPanel1.ScrollControlIntoView(control);
#if NO
                if (this.EnsureVisible != null)
                {
                    EnsureVisibleEventArgs e1 = new EnsureVisibleEventArgs();
                    e1.Control = control;
                    e1.Rect = new Rectangle(control.Location, control.Size);
                    e1.Rect.X += this.flowLayoutPanel1.Location.X;
                    e1.Rect.Y += this.flowLayoutPanel1.Location.Y;
                    this.EnsureVisible(this, e1);
                }
#endif
            }

            // this.BeginInvoke(new Action<Control>(EnsureVisible), control);

            return 0;
        }

        // 将册记录编辑控件加入末尾。注意末尾可能有 Label 控件，要插入在它前面
        void AddEditControl(EntityEditControl edit)
        {
            List<Control> labels = new List<Control>();
            // 先将 Label 标识出来
            foreach (Control control in this.flowLayoutPanel1.Controls)
            {
                if (control is Label)
                    labels.Add(control);
            }

            // 移走 Label
            foreach(Control control in labels)
            {
                this.flowLayoutPanel1.Controls.Remove(control);
            }

            // 追加 edit
            this.flowLayoutPanel1.Controls.Add(edit);

            // 将 label 加到末尾
            foreach(Control control in labels)
            {
                this.flowLayoutPanel1.Controls.Add(control);
            }

            // 注：edit 控件加入到末尾，不会改变前面已有的 edit 控件显示的序号
        }

        void control_PaintContent(object sender, PaintEventArgs e)
        {
            EntityEditControl control = sender as EntityEditControl;

            int index = this.flowLayoutPanel1.Controls.IndexOf(control);
            string strText = (index + 1).ToString();
            using (Brush brush = new SolidBrush(Color.DarkGreen))  // Color.FromArgb(220, 220, 220)
            {
                if (control.CreateState == ItemDisplayState.New
                    || control.CreateState == ItemDisplayState.Deleted)
                {
                    Color state_color = Color.Transparent;
                    if (control.CreateState == ItemDisplayState.New)
                        state_color = Color.FromArgb(0, 200, 0);
                    else if (control.CreateState == ItemDisplayState.Deleted)
                        state_color = Color.FromArgb(200, 200, 200);

                    using (Brush brushState = new SolidBrush(state_color))
                    {
                        int nWidth = 80;
                        Point[] points = new Point[3];
                        points[0] = new Point(0, 0);
                        points[1] = new Point(0, nWidth);
                        points[2] = new Point(nWidth, 0);
                        e.Graphics.FillPolygon(brushState, points);
                    }
                }

                using (Font font = new Font(this.Owner.Font.Name, control.Height / 4, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    SizeF size = e.Graphics.MeasureString(strText, font);
                    // PointF start = new PointF(control.Width / 2, control.Height / 2 - size.Height / 2);
                    PointF start = new PointF(8, 16);

                    StringFormat format = new StringFormat();
                    format.Alignment = StringAlignment.Near;

                    e.Graphics.DrawString(strText, font, brush, start, format);
                }


            }
        }

        void control_GetValueTable(object sender, DigitalPlatform.GetValueTableEventArgs e)
        {
            if (this.GetValueTable != null)
                this.GetValueTable(this, e);    // sender wei
        }

        void control_ContentChanged(object sender, DigitalPlatform.ContentChangedEventArgs e)
        {
            this.m_bEntitiesChanged = true;
        }

        void control_AppendMenu(object sender, AppendMenuEventArgs e)
        {

            MenuItem menuItem = null;

            menuItem = new MenuItem("删除册(&D)");
            menuItem.Tag = sender;
            menuItem.Click += new System.EventHandler(this.menu_deleteItem_Click);
            e.ContextMenu.MenuItems.Add(menuItem);

            // ---
            menuItem = new MenuItem("-");
            e.ContextMenu.MenuItems.Add(menuItem);
        }

        void menu_deleteItem_Click(object sender, EventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            EntityEditControl control = menuItem.Tag as EntityEditControl;

            DialogResult result = MessageBox.Show(this.Owner,
"确实要删除册记录?",
"BiblioRegisterWizard",
MessageBoxButtons.YesNo,
MessageBoxIcon.Question,
MessageBoxDefaultButton.Button2);
            if (result == DialogResult.No)
                return;

            if (string.IsNullOrEmpty(control.RecPath) == false)
            {
                if (this.DeleteItem != null)
                {
                    DeleteItemEventArgs e1 = new DeleteItemEventArgs();
                    e1.Control = control;
                    this.DeleteItem(this, e1);
                    if (string.IsNullOrEmpty(e1.ErrorInfo) == false)
                    {
                        MessageBox.Show(this.Owner, e1.ErrorInfo);
                        return;
                    }
                }
            }

            // this.flowLayoutPanel1.Controls.Remove(control);
            RemoveEditControl(control);
        }

        // 删除一个册记录控件
        public void RemoveEditControl(EntityEditControl edit)
        {
            if (this.easyMarcControl1.InvokeRequired == true)
            {
                this.easyMarcControl1.Invoke(new Action<EntityEditControl>(RemoveEditControl), edit);
                return;
            }

            int index = this.flowLayoutPanel1.Controls.IndexOf(edit);
            this.flowLayoutPanel1.Controls.Remove(edit);

            InvalidateEditControls(index);
        }

        // 要把指定的 Edit 控件后面的全部 Edit 控件 Invalidate 一遍，因为序号发生了变化
        // parameters:
        //      index    从哪个 edit 控件开始刷新。如果为 -1，表示全部刷新
        void InvalidateEditControls(int index)
        {
            if (index == -1)
                index = 0;
            for (int i = index; i < this.flowLayoutPanel1.Controls.Count; i++ )
            {
                Control control = this.flowLayoutPanel1.Controls[i];
                if (control is EntityEditControl)
                    control.Invalidate();
            }
        }

#if NO
        // 要把指定的 Edit 控件后面的全部 Edit 控件 Invalidate 一遍，因为序号发生了变化
        // parameters:
        //      edit    从哪个 edit 控件开始刷新。如果为 null，表示全部刷新
        void InvalidateEditControls(EntityEditControl edit)
        {
            bool bBegin = false;
            foreach (Control control in this.flowLayoutPanel1.Controls)
            {
                if (control == edit || edit == null)
                    bBegin = true;
                if (bBegin == true && control is EntityEditControl)
                    control.Invalidate();
            }
        }
#endif

#if NO
        // 清除临时标签
        void ClearBlank()
        {
            if (this.flowLayoutPanel1.Controls.Count == 1
                && this.flowLayoutPanel1.Controls[0] is Label)
                this.flowLayoutPanel1.Controls.Clear();
        }
#endif

        public void AdjustFlowLayoutHeight()
        {
#if NO
            if (this.easyMarcControl1.InvokeRequired)
            {
                this.easyMarcControl1.Invoke(new Action(AdjustFlowLayoutHeight));
                return;
            }

            Size size = this.flowLayoutPanel1.GetPreferredSize(this.ClientSize);
            int nRow = this.tableLayoutPanel1.GetCellPosition(this.flowLayoutPanel1).Row;
            this.tableLayoutPanel1.RowStyles[nRow] = new RowStyle(SizeType.Absolute,
                size.Height + this.flowLayoutPanel1.Margin.Vertical
                // size.Height + (control != null ? control.Margin.Vertical : 0)
                );
#endif
        }

        // 构造用于保存的实体信息数组
        // parameters:
        //      strAction   change / delete。其中 change 表示新增和修改
        public int BuildSaveEntities(
            string strAction,
            List<EntityEditControl> controls,
            out EntityInfo[] entities,
            out string strError)
        {
            strError = "";
            entities = null;
            int nRet = 0;

            if (controls == null || controls.Count == 0)
            {
                controls = new List<EntityEditControl>();

                foreach (Control control in this.flowLayoutPanel1.Controls)
                {
                    if (!(control is EntityEditControl))
                        continue;

                    EntityEditControl edit = control as EntityEditControl;
                    if (strAction == "change")
                    {
                        if (edit.Changed == false)
                            continue;
                    }

                    controls.Add(edit);
                }
            }

            List<EntityInfo> entityArray = new List<EntityInfo>();

            foreach (EntityEditControl edit in controls)
            {
                EntityInfo info = new EntityInfo();

                if (String.IsNullOrEmpty(edit.RefID) == true)
                {
                    edit.RefID = BookItem.GenRefID();
                }

                info.RefID = edit.RefID;

                if (strAction == "change")
                {
                    string strXml = "";
                    // nRet = edit.GetData(true, out strXml, out strError);
                    nRet = GetEditData(edit, this.BiblioRecPath, out strXml, out strError);
                    if (nRet == -1)
                        return -1;

                    // 试探替换宏
                    // TODO: 如果还有宏没有替换完，应该警告提示
                    nRet = ReplaceEntityMacro(ref strXml,
                        out strError);
                    if (nRet == -1)
                        return -1;
                    if (nRet == 1)
                    {
                        nRet = SetEditData(edit, strXml, edit.RecPath, edit.Timestamp, out strError);
                        if (nRet == -1)
                        {
                            strError = "重新设置 Data 时出错: " + strError;
                            return -1;
                        }
                    }

                    if (string.IsNullOrEmpty(edit.RecPath) == true)
                    {
                        info.Action = "new";
                        info.NewRecPath = "";
                        info.NewRecord = strXml;
                        info.NewTimestamp = null;
                    }
                    else
                    {
                        info.Action = "change";
                        info.OldRecPath = edit.RecPath;
                        info.NewRecPath = edit.RecPath;

                        info.NewRecord = strXml;
                        info.NewTimestamp = null;

                        info.OldRecord = edit.OldRecord;
                        info.OldTimestamp = edit.Timestamp;
                    }
                }
                else if (strAction == "delete")
                {
                    if (string.IsNullOrEmpty(edit.RecPath) == true)
                    {
                        strError = "没有路径的记录无法删除";
                        return -1;
                    }

                    info.Action = "delete";
                    info.OldRecPath = edit.RecPath;
                    info.NewRecPath = edit.RecPath;

                    info.NewRecord = "";
                    info.NewTimestamp = null;

                    info.OldRecord = edit.OldRecord;
                    info.OldTimestamp = edit.Timestamp;
                }

                entityArray.Add(info);
            }

            // 复制到目标
            entities = new EntityInfo[entityArray.Count];
            for (int i = 0; i < entityArray.Count; i++)
            {
                entities[i] = entityArray[i];
            }

            return 0;
        }

        public int ReplaceEntityMacro(ref string strXml,
    out string strError)
        {
            strError = "";

            XmlDocument dom = new XmlDocument();
            try
            {
                dom.LoadXml(strXml);
            }
            catch (Exception ex)
            {
                strError = "XML 装入 DOM 时出错: " + ex.Message;
                return -1;
            }

            int nRet = ReplaceEntityMacro(dom,
                out strError);
            if (nRet == -1)
                return -1;
            strXml = dom.DocumentElement.OuterXml;
            return nRet;
        }

        // 兑现实体记录中的宏
        // return:
        //      -1  出错
        //      0   没有发生修改
        //      1   发生过修改
        public int ReplaceEntityMacro(XmlDocument dom,
            out string strError)
        {
            strError = "";
            bool bChanged = false;

            // 遍历所有一级元素的内容
            XmlNodeList nodes = dom.DocumentElement.SelectNodes("*");
            for (int i = 0; i < nodes.Count; i++)
            {
                string strText = nodes[i].InnerText;
                if (strText.Length > 0 && strText[0] == '@')
                {
                    // 兑现宏
                    string strResult = DoGetMacroValue(strText);
                    if (strResult != strText)
                    {
                        nodes[i].InnerText = strResult;
                        bChanged = true;
                    }
                }
            }

            if (bChanged == true)
                return 1;

            return 0;
        }

        // 兑现 @... 宏值。
        // 如果无法解释宏，则原样返回宏名
        string DoGetMacroValue(string strMacroName)
        {
            if (string.IsNullOrEmpty(this.MarcSyntax) == true)
                return strMacroName;

            if (this.MarcSyntax == "unimarc")
            {
                string strMARC = GetMarc();
                if (string.IsNullOrEmpty(strMARC) == true)
                    return strMacroName;

                MarcRecord record = new MarcRecord(strMARC);

                if (strMacroName == "@price")
                {
                    return record.select("field[@name='010']/subfield[@name='d']").FirstContent;
                }
            }

            if (this.MarcSyntax == "usmarc")
            {
            }

            return strMacroName;
        }

        public void SetMarc(string strMarc)
        {
            this.easyMarcControl1.SetMarc(strMarc);
        }

        public string GetMarc()
        {
            return this.easyMarcControl1.GetMarc();
        }


        delegate int Delegate_SetEditData(EntityEditControl edit,
    string strXml,
    string strRecPath,
    byte[] timestamp,
    out string strError);

        int SetEditData(EntityEditControl edit,
            string strXml,
            string strRecPath,
            byte[] timestamp,
            out string strError)
        {
            if (this.easyMarcControl1.InvokeRequired)
            {
                Delegate_SetEditData d = new Delegate_SetEditData(SetEditData);
                object[] args = new object[5];
                args[0] = edit;
                args[1] = strXml;
                args[2] = strRecPath;
                args[3] = timestamp;
                args[4] = "";
                int result = (int)this.easyMarcControl1.Invoke(d, args);

                // 取出out参数值
                strError = (string)args[4];
                return result;
            }

            return edit.SetData(strXml, strRecPath, timestamp, out strError);
        }

        delegate int Delegate_GetEditData(EntityEditControl edit,
    string strBiblioRecPath,
    out string strXml,
    out string strError);

        int GetEditData(EntityEditControl edit,
    string strBiblioRecPath,
    out string strXml,
    out string strError)
        {
            if (this.easyMarcControl1.InvokeRequired)
            {
                Delegate_GetEditData d = new Delegate_GetEditData(GetEditData);
                object[] args = new object[4];
                args[0] = edit;
                args[1] = strBiblioRecPath;
                args[2] = "";
                args[3] = "";
                int result = (int)this.easyMarcControl1.Invoke(d, args);

                // 取出out参数值
                strXml = (string)args[2];
                strError = (string)args[3];
                return result;
            }

            string strParentID = Global.GetRecordID(strBiblioRecPath);
            if (string.IsNullOrEmpty(strParentID) == true
                || StringUtil.IsNumber(strParentID) == false)
            {
                strXml = "";
                strError = "书目记录路径 '" + strBiblioRecPath + "' 中的记录 ID 部分格式错误";
                return -1;
            }
            edit.ParentId = strParentID;
            return edit.GetData(true, out strXml, out strError);
        }


        // 把报错信息中的成功事项的状态修改兑现
        // 并且彻底去除没有报错的“删除”册事项（内存和视觉上）
        // return:
        //      false   没有警告
        //      true    出现警告
        public bool RefreshOperResult(EntityInfo[] errorinfos,
            out string strWarning)
        {
            int nRet = 0;

            strWarning = ""; // 警告信息

            if (errorinfos == null)
                return false;

            bool bHeightChanged = false;

            foreach (EntityInfo info in errorinfos)
            {

                string strError = "";

                if (String.IsNullOrEmpty(info.RefID) == true)
                {
                    strWarning += " 服务器返回的EntityInfo结构中RefID为空";
                    return true;
                }

                EntityEditControl control = GetEditControl(info.RefID);
                if (String.IsNullOrEmpty(info.RefID) == true)
                {
                    // strWarning += " 定位错误信息 '" + errorinfos[i].ErrorInfo + "' 所在行的过程中发生错误:" + strError;
                    strWarning += " 服务器返回的EntityInfo结构中RefID '" + info.RefID + "' 找不到匹配的控件";
                    return true;
                }

                string strLocationSummary = GetEntitySummary(control);

                // 正常信息处理
                if (info.ErrorCode == ErrorCodeValue.NoError)
                {
                    if (info.Action == "new"
                        || info.Action == "change"
                        || info.Action == "move")
                    {
                        control.OldRecord = info.NewRecord;
#if NO
                        control.SetData(info.NewRecord,
                            info.NewRecPath,
                            info.NewTimestamp,
                            out strError);
#endif
                        nRet = SetEditData(control,
                            info.NewRecord,
                            info.NewRecPath,
                            info.NewTimestamp,
                            out strError);
                        if (nRet == -1)
                        {
                            // MessageBox.Show(ForegroundWindow.Instance, strError);
                            strWarning += " " + strError;
                        }

                        // bookitem.ItemDisplayState = ItemDisplayState.Normal;

                    }

#if NO
                    // 对于保存后变得不再属于本种的，要在listview中消除
                    if (String.IsNullOrEmpty(control.RecPath) == false)
                    {
                        string strTempItemDbName = Global.GetDbName(control.RecPath);
                        string strTempBiblioDbName = "";

                        strTempBiblioDbName = this.MainForm.GetBiblioDbNameFromItemDbName("item", strTempItemDbName);
                        if (string.IsNullOrEmpty(strTempBiblioDbName) == true)
                        {
                            strWarning += " " + this.ItemType + "类型的数据库名 '" + strTempItemDbName + "' 没有找到对应的书目库名";
                            //// MessageBox.Show(ForegroundWindow.Instance, this.ItemType + "类型的数据库名 '" + strTempItemDbName + "' 没有找到对应的书目库名");
                            return true;
                        }
                        string strTempBiblioRecPath = strTempBiblioDbName + "/" + bookitem.Parent;

                        if (strTempBiblioRecPath != this.BiblioRecPath)
                        {
                            this.Items.PhysicalDeleteItem(bookitem);
                            continue;
                        }
                    }
#endif

                    // control.ErrorInfo = "";
                    if (SetEditErrorInfo(control, "") == true)
                        bHeightChanged = true;

                    control.Changed = false;
                    control.CreateState = ItemDisplayState.Normal;

                    continue;
                }

                // 报错处理
                // control.ErrorInfo = info.ErrorInfo;
                if (SetEditErrorInfo(control, info.ErrorInfo) == true)
                    bHeightChanged = true;
                strWarning += strLocationSummary + "在提交保存过程中发生错误 -- " + info.ErrorInfo + "\r\n";
            }

#if NO
            // 最后把没有报错的，那些成功删除事项，都从内存和视觉上抹除
            for (int i = 0; i < this.Items.Count; i++)
            {
                BookItemBase bookitem = this.Items[i];
                if (bookitem.ItemDisplayState == ItemDisplayState.Deleted)
                {
                    if (bookitem.ErrorInfo == "")
                    {
                        this.Items.PhysicalDeleteItem(bookitem);
                        i--;    // 2007/4/12 new add
                    }
                }
            }
#endif
            if (bHeightChanged == true)
                AdjustFlowLayoutHeight();

            // 
            if (String.IsNullOrEmpty(strWarning) == false)
            {
                strWarning += "\r\n请注意修改后重新提交保存";
                //// MessageBox.Show(ForegroundWindow.Instance, strWarning);
                return true;
            }

            return false;
        }

        // 根据参考 ID 找到一个 EntityEditControl
        EntityEditControl GetEditControl(string strRefID)
        {
            foreach (Control control in this.flowLayoutPanel1.Controls)
            {
                if (!(control is EntityEditControl))
                    continue;
                EntityEditControl edit = control as EntityEditControl;
                if (GetEditRefID(edit) == strRefID)
                    return edit;
            }

            return null;
        }

        // 构造事项称呼
        static string GetEntitySummary(EntityEditControl control)
        {
            if (control.InvokeRequired)
            {
                return (string)control.Invoke(new Func<EntityEditControl, string>(GetEntitySummary), control);
            }

            string strBarcode = control.Barcode;

            if (String.IsNullOrEmpty(strBarcode) == false)
                return "册条码号为 '" + strBarcode + "' 的事项";

            string strRegisterNo = control.RegisterNo;

            if (String.IsNullOrEmpty(strRegisterNo) == false)
                return "登录号为 '" + strRegisterNo + "' 的事项";

            string strRecPath = control.RecPath;

            if (String.IsNullOrEmpty(strRecPath) == false)
                return "记录路径为 '" + strRecPath + "' 的事项";

            string strRefID = control.RefID;
            if (String.IsNullOrEmpty(strRefID) == false)
                return "参考ID为 '" + strRefID + "' 的事项";

            return "无任何定位信息的事项";
        }

        // return:
        //      错误信息字符串是否确实被改变
        public static bool SetEditErrorInfo(EntityEditControl edit,
            string strErrorInfo)
        {
            if (edit.InvokeRequired)
            {
                return (bool)edit.Invoke(new Func<EntityEditControl, string, bool>(SetEditErrorInfo), edit, strErrorInfo);
            }
            if (edit.ErrorInfo != strErrorInfo)
            {
                edit.ErrorInfo = strErrorInfo;
                return true;
            }

            return false;
        }

        static string GetEditRefID(EntityEditControl edit)
        {
            if (edit.InvokeRequired)
            {
                return (string)edit.Invoke(new Func<EntityEditControl, string>(GetEditRefID), edit);
            }
            return edit.RefID;
        }

        // 
        /// <summary>
        /// (如果必要，将)册信息部分显示为空
        /// </summary>
        /// <param name="strStyle">not_initial/none</param>
        public void TrySetBlank(string strStyle)
        {
            if (this.easyMarcControl1.InvokeRequired)
            {
                // 事件是在多线程上下文中触发的，需要 Invoke 显示信息
                this.easyMarcControl1.Invoke(new Action<string>(TrySetBlank), strStyle);
                return;
            }

            Label label = null;
            bool bEntity = false;   // 具有至少一个册控件

            foreach (Control control in this.flowLayoutPanel1.Controls)
            {
                if (control is Label)
                    label = control as Label;
                else
                    bEntity = true;

                if (label != null && bEntity == true)
                    break;
            }

            // 如果有册控件了，就不要加入 label 了

            if (bEntity == false)
            {
                if (label == null)
                {
                    label = new Label();
                    string strFontName = "";
                    Font ref_font = GuiUtil.GetDefaultFont();
                    if (ref_font != null)
                        strFontName = ref_font.Name;
                    else
                        strFontName = this.Owner.Font.Name;

                    label.Font = new Font(strFontName, this.Owner.Font.Size * 2, FontStyle.Bold);
                    label.ForeColor = this.flowLayoutPanel1.ForeColor;  // SystemColors.GrayText;


                    label.AutoSize = true;
                    label.Margin = new Padding(8, 8, 8, 8);
                    this.flowLayoutPanel1.Controls.Add(label);
                }

                if (strStyle == "not_initial")
                    label.Text = "册信息尚未初始化";
                else if (strStyle == "none")
                    label.Text = "无册信息";
                else
                {
                    Debug.Assert(false);
                    label.Text = "册信息尚未初始化";
                }

                this.AdjustFlowLayoutHeight();
            }
        }

        public void AddPlus()
        {
            Label label = new Label();
            string strFontName = "";
            Font ref_font = GuiUtil.GetDefaultFont();
            if (ref_font != null)
                strFontName = ref_font.Name;
            else
                strFontName = this.Owner.Font.Name;

            label.Font = new Font(strFontName, this.Owner.Font.Size * 8, FontStyle.Bold);  // 12
            label.ForeColor = this.flowLayoutPanel1.ForeColor;  // SystemColors.GrayText;

            label.AutoSize = true;
            label.Margin = new Padding(8, 8, 8, 8);
            this.flowLayoutPanel1.Controls.Add(label);
            label.Text = "+";
            label.MouseClick += label_MouseClick;
            label.MouseUp += label_MouseUp;
            label.BackColor = ControlPaint.Dark(this.flowLayoutPanel1.BackColor);
            label.TextAlign = ContentAlignment.MiddleCenter;
        }

        void label_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            string strError = "";
            int nRet = AddNewEntity("", out strError);
            if (nRet == -1)
                MessageBox.Show(this.Owner, strError);        }

        void label_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            ContextMenu contextMenu = new ContextMenu();
            MenuItem menuItem = null;

            for (int i = 1; i <= 10; i++)
            {
                menuItem = new MenuItem("新增 " + i.ToString() + " 个册");
                menuItem.Tag = i;
                menuItem.Click += new System.EventHandler(this.menu_newMultipleEntities_Click);
                contextMenu.MenuItems.Add(menuItem);
            }

            contextMenu.Show(sender as Control, new Point(e.X, e.Y));
        }

        void menu_newMultipleEntities_Click(object sender, EventArgs e)
        {
            string strError = "";
            MenuItem menu = sender as MenuItem;
            int n = (int)menu.Tag;
            for (int i = 0; i < n; i++)
            {
                int nRet = AddNewEntity("", out strError);
                if (nRet == -1)
                    goto ERROR1;
            }
            return;
        ERROR1:
            MessageBox.Show(this.Owner, strError);
        }

        // 新添加一个实体事项
        public int AddNewEntity(string strText,
            out string strError)
        {
            strError = "";
            int nRet = 0;

            string strQuickDefault = "<root />";
            if (this.GetDefaultItem != null)
            {
                GetDefaultItemEventArgs e = new GetDefaultItemEventArgs();
                this.GetDefaultItem(this, e);
                if (string.IsNullOrEmpty(e.ErrorInfo) == false)
                {
                    strError = e.ErrorInfo;
                    return -1;
                }
            }
            // 根据缺省值，构造最初的 XML
            XmlDocument dom = new XmlDocument();
            try
            {
                dom.LoadXml(strQuickDefault);
            }
            catch (Exception ex)
            {
                strError = "缺省册记录装入 XMLDOM 时出错: " + ex.Message;
                return -1;
            }

            DomUtil.SetElementText(dom.DocumentElement, "barcode", strText);
            DomUtil.SetElementText(dom.DocumentElement, "refID", Guid.NewGuid().ToString());

            // 兑现 @price 宏，如果有书目记录的话
            // TODO: 当书目记录切换的时候，是否还要重新兑现一次宏?

            // 记录路径可以保持为空，直到保存前才构造
            nRet = ReplaceEntityMacro(dom,
                out strError);
            if (nRet == -1)
                return -1;
            // 添加一个新的册对象
            nRet = NewEntity(dom.DocumentElement.OuterXml,
                out strError);
            if (nRet == -1)
                return -1;

            return 0;
        }

        // 添加一个新的册对象
        public int NewEntity(string strXml,
            out string strError)
        {
            return this.NewEntity("",
                null,
                strXml,
                true,
                out strError);
        }

    }

    /// <summary>
    /// 获得缺省册记录事件
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="e">事件参数</param>
    public delegate void GetDefaultItemEventHandler(object sender,
    GetDefaultItemEventArgs e);

    /// <summary>
    /// 获得缺省册记录事件的参数
    /// </summary>
    public class GetDefaultItemEventArgs : EventArgs
    {
        public string Xml = "";         // [out]
        public string ErrorInfo = "";   // [out]
    }
}
