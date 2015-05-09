﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Xml;

using DigitalPlatform.CommonControl;
using DigitalPlatform.Marc;
using DigitalPlatform.Xml;
using DigitalPlatform.Text;
using System.Drawing.Drawing2D;

namespace DigitalPlatform.EasyMarc
{
    /// <summary>
    /// MARC 模板输入界面控件
    /// </summary>
    public partial class EasyMarcControl : UserControl
    {



        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        public new event EventHandler TextChanged;

        Font _fixedFont = null;

        internal Font FixedFont 
        {
            get
            {
                if (this._fixedFont == null)
                    this._fixedFont = new Font("Courier New", this.Font.Size);
                return this._fixedFont;
            }
        }

        public override Font Font
        {
            get
            {
                return base.Font;
            }
            set
            {
                base.Font = value;

                this._fixedFont = null;
            }
        }

        public EasyMarcControl()
        {
            this.DoubleBuffered = true;

            InitializeComponent();
        }

        /// <summary>
        /// 解析宏
        /// </summary>
        public event ParseMacroEventHandler ParseMacro = null;


        // 原始的行数组
        public List<EasyLine> Items = new List<EasyLine>();

        bool m_bChanged = false;

        /// <summary>
        /// 内容是否发生过修改
        /// </summary>
        [Category("Content")]
        [DescriptionAttribute("Changed")]
        [DefaultValue(false)]
        public bool Changed
        {
            get
            {
                return this.m_bChanged;
            }
            set
            {
                if (this.m_bChanged != value)
                {
                    this.m_bChanged = value;

                    if (value == false)
                        ResetLineState();
                }
            }
        }

        bool m_bHideSelection = true;


        [Category("Appearance")]
        [DescriptionAttribute("HideSelection")]
        [DefaultValue(true)]
        public bool HideSelection
        {
            get
            {
                return this.m_bHideSelection;
            }
            set
            {
                if (this.m_bHideSelection != value)
                {
                    this.m_bHideSelection = value;
                    this.RefreshLineColor(); // 迫使颜色改变
                }
            }
        }

        void RefreshLineColor()
        {
            for (int i = 0; i < this.Items.Count; i++)
            {
                EasyLine item = this.Items[i];
                item.SetLineColor();
            }
        }

        // 将全部行的状态恢复为普通状态
        void ResetLineState()
        {
            for (int i = 0; i < this.Items.Count; i++)
            {
                EasyLine item = this.Items[i];

                if ((item.State & ItemState.ReadOnly) != 0)
                    item.State = ItemState.Normal | ItemState.ReadOnly;
                else
                    item.State = ItemState.Normal;
            }

            this.Invalidate();
        }

        bool _hideIndicator = true;
        public bool HideIndicator
        {
            get
            {
                return this._hideIndicator;
            }
            set
            {
                if (this._hideIndicator != value)
                {
                    this._hideIndicator = value;
                    foreach(EasyLine line in this.Items)
                    {
                        if (line is FieldLine)
                        {
                            FieldLine field = line as FieldLine;
                            if (field.IsControlField == false)
                                field.textBox_content.Visible = !value;
                        }
                    }
                }
            }
        }

#if NO
        bool _hideFields = false;
        public bool HideFields
        {
            get
            {
                return this._hideFields;
            }
            set
            {
                this._hideFields = value;
            }
        }
#endif

        public List<EasyLine> SelectedItems
        {
            get
            {
                List<EasyLine> results = new List<EasyLine>();

                for (int i = 0; i < this.Items.Count; i++)
                {
                    EasyLine cur_element = this.Items[i];
                    if ((cur_element.State & ItemState.Selected) != 0)
                        results.Add(cur_element);
                }

                return results;
            }
        }

        public List<int> SelectedIndices
        {
            get
            {
                List<int> results = new List<int>();

                for (int i = 0; i < this.Items.Count; i++)
                {
                    EasyLine cur_element = this.Items[i];
                    if ((cur_element.State & ItemState.Selected) != 0)
                        results.Add(i);
                }

                return results;
            }
        }

        public void SelectAll()
        {
            for (int i = 0; i < this.Items.Count; i++)
            {
                EasyLine cur_element = this.Items[i];
                if ((cur_element.State & ItemState.Selected) == 0)
                    cur_element.State |= ItemState.Selected;
            }

            this.Invalidate();
        }

        public TableLayoutPanel TableLayoutPanel
        {
            get
            {
                return this.tableLayoutPanel_content;
            }
        }

        public void EnsureVisible(EasyLine item)
        {
            int[] row_heights = this.tableLayoutPanel_content.GetRowHeights();
            int nYOffs = 0; // row_heights[0];
            int i = 0;  // 1
            foreach (EasyLine cur_item in this.Items)
            {
                if (cur_item == item)
                    break;
                nYOffs += row_heights[i++];
            }

            // this.AutoScrollPosition = new Point(this.AutoScrollOffset.X, 1000);
            if (nYOffs < - this.AutoScrollPosition.Y)
            {
                this.AutoScrollPosition = new Point(this.AutoScrollOffset.X, nYOffs);
            }
            else if (nYOffs + row_heights[i] > - (this.AutoScrollPosition.Y - this.ClientSize.Height))
            {
                // 刚好进入下部
                this.AutoScrollPosition = new Point(this.AutoScrollOffset.X, nYOffs - this.ClientSize.Height + row_heights[i]);
            }
        }

        public ImageList ImageListIcons
        {
            get
            {
                return this.imageList_expandIcons;
            }
        }

        // 文档发生改变
        internal void FireTextChanged()
        {
            this.Changed = true;

            EventArgs e = new EventArgs();
            // this.OnTextChanged(e);
            if (this.TextChanged != null)
                this.TextChanged(this, e);
        }

        // 找到一个子字段所从属的字段行
        FieldLine GetFieldLine(SubfieldLine subfield)
        {
            int nStart = this.Items.IndexOf(subfield);
            if (nStart == -1)
                return null;
            for (int i = nStart - 1; i >= 0; i--)
            {
                EasyLine line = this.Items[i];
                if (line is FieldLine)
                    return line as FieldLine;
            }

            return null;
        }

        // 获得一个字段行下属的全部子字段行
        List<EasyLine> GetSubfieldLines(FieldLine field)
        {
            List<EasyLine> results = new List<EasyLine>();
            int nStart = this.Items.IndexOf(field);
            if (nStart == -1)
                return results;
            for (int i = nStart + 1; i < this.Items.Count; i++)
            {
                EasyLine line = this.Items[i];
                if (line is FieldLine)
                    break;
                results.Add(line);
            }

            return results;
        }

        // 删除若干行
        // 如果其中包含字段行，则要把字段下属的子字段行一并删除
        public void DeleteElements(List<EasyLine> selected_lines)
        {
            this.DisableUpdate();
            try
            {
                bool bChanged = false;
                List<EasyLine> deleted_subfields = new List<EasyLine>();
                // 先删除里面的 Field 行
                foreach (EasyLine line in selected_lines)
                {
                    if (line is FieldLine)
                    {
                        List<EasyLine> subfields = GetSubfieldLines(line as FieldLine);

                        this.RemoveItem(line, false);
                        bChanged = true;
                        foreach (EasyLine subfield in subfields)
                        {
                            this.RemoveItem(subfield, false);
                            bChanged = true;
                        }
                        deleted_subfields.AddRange(subfields);
                    }
                }

                // 然后删除零星的子字段行
                foreach (EasyLine line in selected_lines)
                {
                    if (line is SubfieldLine)
                    {
                        if (deleted_subfields.IndexOf(line) == -1)
                        {
                            this.RemoveItem(line, false);
                            bChanged = true;
                        }
                    }
                }

                if (bChanged == true)
                    this.FireTextChanged();
            }
            finally
            {
                this.EnableUpdate();
            }
        }

        EasyLine LastClickItem = null;   // 最近一次click选择过的Item对象

        public void SelectItem(EasyLine element,
            bool bClearOld)
        {

            if (bClearOld == true)
            {
                for (int i = 0; i < this.Items.Count; i++)
                {
                    EasyLine cur_element = this.Items[i];

                    if (cur_element == element)
                        continue;   // 暂时不处理当前行

                    if ((cur_element.State & ItemState.Selected) != 0)
                    {
                        cur_element.State -= ItemState.Selected;

                        this.InvalidateLine(cur_element);
                    }
                }

                // 2014/9/30
                if (element.textBox_content.Visible == true)
                {
                    element.textBox_content.Focus();
                    element.textBox_content.Select(0, 0);
                }
            }

            // 选中当前行
            if ((element.State & ItemState.Selected) == 0)
            {
                element.State |= ItemState.Selected;

                this.InvalidateLine(element);
            }

            this.LastClickItem = element;
        }

        public void ToggleSelectItem(EasyLine element)
        {
            // 选中当前行
            if ((element.State & ItemState.Selected) == 0)
                element.State |= ItemState.Selected;
            else
                element.State -= ItemState.Selected;

            this.InvalidateLine(element);

            this.LastClickItem = element;
        }

        public void RangeSelectItem(EasyLine element)
        {
            EasyLine start = this.LastClickItem;

            int nStart = this.Items.IndexOf(start);
            if (nStart == -1)
                return;

            int nEnd = this.Items.IndexOf(element);

            if (nStart > nEnd)
            {
                // 交换
                int nTemp = nStart;
                nStart = nEnd;
                nEnd = nTemp;
            }

            for (int i = nStart; i <= nEnd; i++)
            {
                EasyLine cur_element = this.Items[i];

                if ((cur_element.State & ItemState.Selected) == 0)
                {
                    cur_element.State |= ItemState.Selected;

                    this.InvalidateLine(cur_element);
                }
            }

            // 清除其余位置
            for (int i = 0; i < nStart; i++)
            {
                EasyLine cur_element = this.Items[i];

                if ((cur_element.State & ItemState.Selected) != 0)
                {
                    cur_element.State -= ItemState.Selected;

                    this.InvalidateLine(cur_element);
                }
            }

            for (int i = nEnd + 1; i < this.Items.Count; i++)
            {
                EasyLine cur_element = this.Items[i];

                if ((cur_element.State & ItemState.Selected) != 0)
                {
                    cur_element.State -= ItemState.Selected;

                    this.InvalidateLine(cur_element);
                }
            }
        }

        public List<string> HideFieldNames = new List<string>();

        internal int GetHideFieldCount()
        {
            int nCount = 0;
            foreach (EasyLine line in this.Items)
            {
                if (line is FieldLine)
                {
                    FieldLine field = line as FieldLine;
                    if (field.Visible == false)
                        nCount++;
                }
            }

            return nCount;
        }

        // 将指定字段名的字段改变隐藏状态
        // parameters:
        //      field_names 要施加影响的字段名。如果为 null 表示全部
        public void HideFields(List<string> field_names, bool bHide)
        {
            this.DisableUpdate();
            try
            {
                foreach (EasyLine line in this.Items)
                {
                    if (line is FieldLine)
                    {
                        FieldLine field = line as FieldLine;
                        if (field_names != null && field_names.IndexOf(field.Name) == -1)
                            continue;
                        if (field.Visible == bHide)
                            ToggleHide(field);
                    }
                }
            }
            finally
            {
                this.EnableUpdate();
            }
        }

#if NO
        public void VisibleAllFields()
        {
            this.DisableUpdate();
            try
            {
                foreach (EasyLine line in this.Items)
                {
                    if (line is FieldLine)
                    {
                        FieldLine field = line as FieldLine;
                        if (field.Visible == false)
                            ToggleHide(field);
                    }
                }
            }
            finally
            {
                this.EnableUpdate();
            }
        }
#endif

        internal void ToggleHide(FieldLine field)
        {
            this.DisableUpdate();
            try
            {
                bool bNewValue = !field.Visible;

                field.Visible = bNewValue;


                int nStart = this.Items.IndexOf(field);
                if (nStart == -1)
                    return;

                if (field.ExpandState != ExpandState.Collapsed)
                {
                    // 将下属子字段显示或者隐藏
                    for (int i = nStart + 1; i < this.Items.Count; i++)
                    {
                        EasyLine current = this.Items[i];
                        if (current is FieldLine)
                            break;
                        if (current.Visible != bNewValue)
                            current.Visible = bNewValue;
                    }
                }
            }
            finally
            {
                this.EnableUpdate();
            }
        }

        internal void ToggleExpand(EasyLine line)
        {
            if (line.ExpandState == EasyMarc.ExpandState.None)
                return;

            this.DisableUpdate();
            try
            {

                if (line.ExpandState == EasyMarc.ExpandState.Expanded)
                    line.ExpandState = EasyMarc.ExpandState.Collapsed;
                else
                    line.ExpandState = EasyMarc.ExpandState.Expanded;

                int nStart = this.Items.IndexOf(line);
                if (nStart == -1)
                    return;

                // 将下属子字段显示或者隐藏
                for (int i = nStart + 1; i < this.Items.Count; i++)
                {
                    EasyLine current = this.Items[i];
                    if (current is FieldLine)
                        break;
                    if (line.ExpandState == ExpandState.Expanded)
                        current.Visible = true;
                    else
                        current.Visible = false;
                }

            }
            finally
            {
                this.EnableUpdate();
            }
        }

        public void ExpandAll(bool bExpand)
        {
            this.DisableUpdate();
            try
            {
                foreach (EasyLine line in this.Items)
                {
                    if (line.ExpandState != ExpandState.None)
                    {
                        if ((bExpand == true && line.ExpandState == ExpandState.Collapsed)
                            || (bExpand == false && line.ExpandState == ExpandState.Expanded))
                            ToggleExpand(line);
                    }
                }
            }
            finally
            {
                this.EnableUpdate();
            }
        }

        public int CaptionWidth
        {
            get
            {
                return (int)this.tableLayoutPanel_content.ColumnStyles[1].Width;
            }
            set
            {
                int nNewWidth = value;
                // TODO: 当空间宽度很小的时候，可能就设置不了希望的更大宽度了。似乎应该放开限制
                nNewWidth = Math.Min(nNewWidth, this.tableLayoutPanel_content.Width / 2);
                nNewWidth = Math.Max(nNewWidth, 70);
                this.tableLayoutPanel_content.ColumnStyles[1].Width = nNewWidth;
            }
        }

        public void ChangeCaptionWidth(int nDelta)
        {
            int nOldWidth = (int)this.tableLayoutPanel_content.ColumnStyles[1].Width;
            int nNewWidth = nOldWidth + nDelta;
            nNewWidth = Math.Min(nNewWidth, this.tableLayoutPanel_content.Width / 2);
            nNewWidth = Math.Max(nNewWidth, 70);
            this.tableLayoutPanel_content.ColumnStyles[1].Width = nNewWidth;
        }

        internal void InvalidateLine(EasyLine item)
        {
            Point p = this.tableLayoutPanel_content.PointToScreen(new Point(0, 0));

            Rectangle rect = item.label_color.RectangleToScreen(item.label_color.ClientRectangle);
            rect.Width = this.tableLayoutPanel_content.DisplayRectangle.Width;
            rect.Offset(-p.X, -p.Y);
            rect.Height = (int)this.Font.GetHeight() + 8;   // 缩小刷新高度

            this.tableLayoutPanel_content.Invalidate(rect, false);

            // this.tableLayoutPanel_content.Invalidate();
        }


        public void RemoveItem(EasyLine line, bool bFireEvent)
        {
            int index = this.Items.IndexOf(line);

            if (index == -1)
                return;

            line.RemoveFromTable(this.tableLayoutPanel_content, index);

            this.Items.Remove(line);

            // this.Changed = true;
            if (bFireEvent == true)
                this.FireTextChanged();
        }

        public void Clear()
        {
            this.DisableUpdate();

            try
            {
                for (int i = 0; i < this.tableLayoutPanel_content.RowStyles.Count; i++)
                {
                    for (int j = 0; j < this.tableLayoutPanel_content.ColumnStyles.Count; j++)
                    {
                        Control control = this.tableLayoutPanel_content.GetControlFromPosition(j, i);
                        if (control != null)
                            this.tableLayoutPanel_content.Controls.Remove(control);
                    }
                }

#if NO
                for (int i = 0; i < this.Items.Count; i++)
                {
                    EasyLine element = this.Items[i];
                    ClearOneItemControls(this.tableLayoutPanel_content,
                        element);
                }
#endif

                this.Items.Clear();
                this.tableLayoutPanel_content.RowCount = 2;    // 为什么是2？
                for (; ; )
                {
                    if (this.tableLayoutPanel_content.RowStyles.Count <= 2)
                        break;
                    this.tableLayoutPanel_content.RowStyles.RemoveAt(2);
                }
            }
            finally
            {
                this.EnableUpdate();
            }
        }

#if NO
        // 清除一个Item对象对应的Control
        internal void ClearOneItemControls(
            TableLayoutPanel table,
            EasyLine line)
        {
            table.Controls.Remove(line.label_color);

            table.Controls.Remove(line.label_caption);

            if (line.splitter != null)
                table.Controls.Remove(line.splitter);

            table.Controls.Remove(line.textBox_content);
        }
#endif

        string _header = "";

        // 设置 MARC 记录内容
        public void SetMarc(string strMarc)
        {
            this.Clear();

            this.DisableUpdate();
            try
            {
                int nLineIndex = 0;
                MarcRecord record = new MarcRecord(strMarc);
                this._header = record.Header.ToString();    // 头标区
                foreach (MarcField field in record.ChildNodes)
                {
                    FieldLine field_line = new FieldLine(this);
                    field_line.Name = field.Name;
                    field_line.Caption = GetCaption(field.Name, "", this._includeNumber);
                    // field_line.Indicator = field.Indicator;
                    InsertNewLine(nLineIndex++,
                        field_line,
                        false);
                    field_line.IsControlField = field.IsControlField;
                    if (field.IsControlField == true)
                    {
                        field_line.Content = field.Content;
                        field_line.ExpandState = ExpandState.None;
                    }
                    else
                    {
#if NO
                        // 指示符行
                        IndicatorLine indicator_line = new IndicatorLine(this);
                        indicator_line.Name = "";
                        indicator_line.Caption = "指示符";
                        indicator_line.Content = field.Indicator;
                        InsertNewLine(nLineIndex++,
                            indicator_line);
#endif
                        field_line.Indicator = field.Indicator;

                        foreach (MarcSubfield subfield in field.ChildNodes)
                        {
                            SubfieldLine subfield_line = new SubfieldLine(this);
                            subfield_line.Name = subfield.Name;
                            subfield_line.Caption = GetCaption(field.Name, subfield.Name, this._includeNumber);
                            subfield_line.Content = subfield.Content;
                            InsertNewLine(nLineIndex++,
                                subfield_line,
                                false);
                        }

                        field_line.ExpandState = ExpandState.Expanded;
                    }
                }

                this.Changed = false;
                ResetLineState();
            }
            finally
            {
                this.EnableUpdate();
            }
        }

        #region 插入子字段功能

        public string DefaultSubfieldName = "a";

        // 插入一个新的子字段
        public SubfieldLine NewSubfield(int index)
        {
            EasyLine ref_line = this.Items[index];
            string strFieldName = "";
            if (ref_line is FieldLine)
            {
                // 只能后插 TODO: 让对话框不能选择前插
                strFieldName = (ref_line as FieldLine).Name;
            }
            else
            {
                FieldLine ref_field_line = GetFieldLine(ref_line as SubfieldLine);
                if (ref_field_line == null)
                    return null;    // 应该抛出异常?
                strFieldName = ref_field_line.Name;
            }

            // 询问子字段名
            NewSubfieldDialog dlg = new NewSubfieldDialog();
            dlg.Font = this.Font;
            dlg.Text = "新子字段";
            dlg.AutoComplete = true;

            dlg.ParentNameString = strFieldName;
            dlg.NameString = this.DefaultSubfieldName;
            dlg.MarcDefDom = this.MarcDefDom;
            dlg.Lang = this.Lang;

            dlg.StartPosition = FormStartPosition.CenterScreen;
            dlg.ShowDialog(this);

            if (dlg.DialogResult != DialogResult.OK)
                return null;

            if (ref_line is FieldLine)
            {
                // 只能后插
                dlg.InsertBefore = false;
            }

            string strDefaultValue = "";

            List<string> results = null;
            string strError = "";
            // 获得宏值
            // parameters:
            //      strSubFieldName 子字段名。特殊地，如果为"#indicator"，表示想获取该字段的指示符缺省值
            // return:
            //      -1  error
            //      0   not found 
            //      1   found
            int nRet = GetDefaultValue(
                0,  // index,
                dlg.ParentNameString,
                dlg.NameString,
                out results,
                out strError);
            if (nRet == -1)
                MessageBox.Show(this, strError);

            if (results != null
                && results.Count > 0)
            {
                strDefaultValue = results[0];
            }

            bool bChanged = false;

            SubfieldLine subfield_line = new SubfieldLine(this);
            subfield_line.Name = dlg.NameString;
            subfield_line.Caption = GetCaption(dlg.ParentNameString, dlg.NameString, this._includeNumber);
            subfield_line.Content = strDefaultValue;
            if (dlg.InsertBefore == true)
            {
                InsertNewLine(index++,
                    subfield_line,
                    false);
                bChanged = true;
            }
            else
            {
                index++;
                InsertNewLine(index++,
                    subfield_line,
                    false);
                bChanged = true;
            }

            if (bChanged == true)
                this.FireTextChanged();

            return subfield_line;
        }


        #endregion

        #region 插入字段功能

        public string DefaultFieldName = "???";

        // 插入一个新的字段
        public FieldLine NewField(int index)
        {
            EasyLine ref_line = null;
            
            if (index < this.Items.Count)
                ref_line = this.Items[index];

            if (ref_line is SubfieldLine)
            {
                ref_line = GetFieldLine(ref_line as SubfieldLine);
                if (ref_line == null)
                {
                    throw new Exception("index 为 "+index.ToString()+" 的子字段行没有找到字段行");
                }
                index = this.Items.IndexOf(ref_line);
                Debug.Assert(index != -1, "");
            }

            // 询问字段名
            NewSubfieldDialog dlg = new NewSubfieldDialog();
            dlg.Font = this.Font;

            dlg.Text = "新字段";
            dlg.AutoComplete = true;

            dlg.NameString = this.DefaultFieldName;
            dlg.MarcDefDom = this.MarcDefDom;
            dlg.Lang = this.Lang;

            dlg.StartPosition = FormStartPosition.CenterScreen;
            dlg.ShowDialog(this);

            if (dlg.DialogResult != DialogResult.OK)
                return null;

            bool bControlField = Record.IsControlFieldName(dlg.NameString);
            string strDefaultValue = "";
            string strIndicator = "  ";
            if (bControlField == false)
                strDefaultValue = new string((char)31, 1) + "a";

            List<string> results = null;
            string strError = "";
            // 获得宏值
            // parameters:
            //      strSubFieldName 子字段名。特殊地，如果为"#indicator"，表示想获取该字段的指示符缺省值
            // return:
            //      -1  error
            //      0   not found 
            //      1   found
            int nRet = GetDefaultValue(
                0,  // index,
                dlg.NameString,
                "",
                out results,
                out strError);
            if (nRet == -1)
                MessageBox.Show(this, strError);

            if (results != null
                && results.Count > 0)
            {
                strDefaultValue = results[0];
                if (bControlField == false)
                    SplitDefaultValue(strDefaultValue,
                        out strIndicator,
                        out strDefaultValue);
            }

            bool bChanged = false;

            // 辅助剖析子字段的对象
            MarcField field_node = new MarcField(dlg.NameString, strIndicator, strDefaultValue);

            FieldLine field_line = new FieldLine(this);
            field_line.IsControlField = bControlField;
            field_line.Name = dlg.NameString;
            field_line.Caption = GetCaption(dlg.NameString, "", this._includeNumber);
            if (bControlField == false)
            {
                field_line.Indicator = strIndicator;  // 从 marcdef 中获得缺省的指示符值
                field_line.ExpandState = ExpandState.Expanded;
            }

            EasyLine after_line = null;
            bool bExpanded = false;
            bool bHideChanged = false;
            if (dlg.InsertBefore == true)
            {
                after_line = ref_line;
            }
            else
            {
                // 如果插入点是字段行，需要跳过下属的子字段行
                // EasyLine ref_line = this.Items[index];
                if (ref_line is FieldLine)
                {
                    List<EasyLine> lines = GetSubfieldLines(ref_line as FieldLine);
                    index += lines.Count + 1;
                }
                else
                    index++;

                if (index < this.Items.Count)
                    after_line = this.Items[index];
            }

            // 如果插入位置后面一个字段是隐藏状态，则会出现故障，需要先修改为显示状态，插入后再隐藏
            if (after_line is FieldLine && after_line.Visible == false)
            {
                this.ToggleHide(after_line as FieldLine);
                bHideChanged = true;
            }
            // 如果本字段是收缩状态，则会出现故障，需要先修改为展开状态，插入后再收缩
            if (after_line.ExpandState == ExpandState.Collapsed)
            {
                this.ToggleExpand(after_line);
                Debug.Assert(after_line.ExpandState == ExpandState.Expanded, "");
                bExpanded = true;
            }

            InsertNewLine(index++,
    field_line,
    false);
            bChanged = true;

            // 如果必要，创建子字段对象
            foreach (MarcSubfield subfield_node in field_node.ChildNodes)
            {
                SubfieldLine subfield_line = new SubfieldLine(this);
                subfield_line.Name = subfield_node.Name;
                subfield_line.Caption = GetCaption(field_node.Name, subfield_node.Name, this._includeNumber);
                subfield_line.Content = subfield_node.Content;
                InsertNewLine(index++,
                    subfield_line,
                    false);
                bChanged = true;
            }

            // 把参考行恢复到以前的状态
            if (after_line != null && bExpanded == true)
            {
                this.ToggleExpand(after_line);
                Debug.Assert(after_line.ExpandState == ExpandState.Collapsed, "");
            }
            if (after_line != null && bHideChanged == true)
            {
                this.ToggleHide(after_line as FieldLine);
            }
            if (bChanged == true)
                this.FireTextChanged();

            return field_line;
        }

        const char SUBFLD = (char)31;

        // 从缺省值字符串中分离出字段指示符和纯粹字段内容部分
        // 函数调用前，strText中可能含有指示符，也可能没有
        static void SplitDefaultValue(string strText,
                        out string strIndicator,
                        out string strContent)
        {
            strIndicator = "  ";
            strContent = "";

            if (string.IsNullOrEmpty(strText) == true)
                return;

            int nRet = strText.IndexOf(SUBFLD);
            if (nRet == -1)
            {
                if (strText.Length < 2)
                {
                    strContent = strText;
                    return;
                }

                strIndicator = strText.Substring(0, 2);
                strContent = strText.Substring(2);
                return;
            }

            if (nRet >= 2)
            {
                strIndicator = strText.Substring(0, 2);
                strContent = strText.Substring(2);
                return;
            }

            strContent = strText;
        }

        // 获得宏值
        // parameters:
        //      nPushIndex  需要实做的字符串事项的下标。如果为-1，表示没有要实做的事项(即全部都是模拟)
        //      strSubFieldName 子字段名。特殊地，如果为"#indicator"，表示想获取该字段的指示符缺省值
        // return:
        //      -1  error
        //      0   not found 
        //      >0  found 结果的个数
        private int GetDefaultValue(
            // bool bSimulate,
            int nPushIndex,
            string strFieldName,
            string strSubFieldName,
            out List<string> results,
            out string strError)
        {
            Debug.Assert(strFieldName != null, "strFieldName参数不能为null");
            Debug.Assert(strSubFieldName != null, "strSubFieldName参数不能为null");
            // Debug.Assert(strValue != null, "strValue参数不能为null");

            strError = "";
            results = new List<string>();

            // 检查MarcDefDom是否存在
            if (this.MarcDefDom == null)
            {
                strError = m_strMarcDomError;
                return -1;
            }

            // 根据字段名找到配置文件中的该字段的定义
            XmlNode node = null;

            if (strSubFieldName == "" || strSubFieldName == "#indicator")
            {
                // 只找到字段
                node = this.MarcDefDom.DocumentElement.SelectSingleNode("Field[@name='" + strFieldName + "']");
            }
            else
            {
                // 找到子字段
                node = this.MarcDefDom.DocumentElement.SelectSingleNode("Field[@name='" + strFieldName + "']/Subfield[@name='" + strSubFieldName + "']");
            }

            if (node == null)
            {
                return 0;   // not found def
            }

            XmlNodeList value_nodes = null;

            if (strSubFieldName == "#indicator")
            {

                value_nodes = node.SelectNodes("Property/Indicator/Property/DefaultValue");
            }
            else
            {
                value_nodes = node.SelectNodes("Property/DefaultValue");
            }

            if (value_nodes.Count == 0)
                return 0;

            for (int i = 0; i < value_nodes.Count; i++)
            {
                string strOutputValue = value_nodes[i].InnerText;

                // 去掉定义值中的\r\n或者单独的\r和\n。这种具有\r\n的效果可能由notepad中折行状态时paste到编辑配置文件对话框并保存来造成.
                strOutputValue = strOutputValue.Replace("\r", "");
                strOutputValue = strOutputValue.Replace("\n", "");

                // 子字段符号
                strOutputValue = strOutputValue.Replace("\\", new string((char)31, 1));

                ParseMacroEventArgs e = new ParseMacroEventArgs();
                e.Macro = strOutputValue;
                // e.Simulate = bSimulate;
                if (i == nPushIndex)
                    e.Simulate = false; // 实做
                else
                    e.Simulate = true;  // 模拟

                TemplateControl_ParseMacro((object)this, e);
                if (String.IsNullOrEmpty(e.ErrorInfo) == false)
                {
                    strError = e.ErrorInfo;
                    return -1;
                }

                strOutputValue = e.Value;

                /*
                strOutputValue = MacroTimeValue(strOutputValue);

                // 替换下划线
                strOutputValue = strOutputValue.Replace("_", " ");


                if (strSubFieldName == "")
                {
                    // 替换子字段符号
                    strOutputValue = strOutputValue.Replace('$', Record.SUBFLD);
                }
                 * */

                results.Add(strOutputValue);
            }

            return results.Count;
        }

        // 兑现宏
        void TemplateControl_ParseMacro(object sender, ParseMacroEventArgs e)
        {
            // 将一些基本的宏兑现
            // %year%%m2%%d2%%h2%%min2%%sec2%.%hsec%

            string strOutputValue = MacroTimeValue(e.Macro);

            // 替换下划线
            // 只替换前面连续的'_'
            // strOutputValue = strOutputValue.Replace("_", " ");

            // 替换字符串最前面一段连续的字符
            strOutputValue = StringUtil.ReplaceContinue(strOutputValue, '_', ' ');

            // 替换子字段符号
            strOutputValue = strOutputValue.Replace(Record.KERNEL_SUBFLD, Record.SUBFLD);   // $?

            e.Value = strOutputValue;
            e.ErrorInfo = "";

            // 如果是一般的宏, MARC编辑器控件就可以解决
            // 如果控件外围没有支持事件, 也只能这里解决部分
            if (e.Macro.IndexOf("%") == -1 || this.ParseMacro == null)
            {
                return;
            }
            else
            {
                // 否则还需要求助于宿主
                ParseMacroEventArgs e1 = new ParseMacroEventArgs();
                e1.Macro = e.Value; // 第一次处理过的, 再级联处理
                e1.Simulate = e.Simulate;
                this.ParseMacro(this, e1);

                e.Value = e1.Value;
                e.ErrorInfo = e1.ErrorInfo;
                return;
            }
        }

        /// <summary>
        /// 兑现时间宏值
        /// </summary>
        /// <param name="strMacro">要处理的宏字符串</param>
        /// <returns>兑现宏以后的字符串</returns>
        public static string MacroTimeValue(string strMacro)
        {
            DateTime time = DateTime.Now;

            // utime
            strMacro = strMacro.Replace("%utime%", time.ToString("u"));

            // 年 year
            strMacro = strMacro.Replace("%year%", Convert.ToString(time.Year).PadLeft(4, '0'));

            // 年 y2
            strMacro = strMacro.Replace("%y2%", time.Year.ToString().PadLeft(4, '0').Substring(2, 2));

            // 月 month
            strMacro = strMacro.Replace("%month%", Convert.ToString(time.Month));

            // 月 m2
            strMacro = strMacro.Replace("%m2%", Convert.ToString(time.Month).PadLeft(2, '0'));

            // 日 day
            strMacro = strMacro.Replace("%day%", Convert.ToString(time.Day));

            // 日 d2
            strMacro = strMacro.Replace("%d2%", Convert.ToString(time.Day).PadLeft(2, '0'));

            // 时 hour
            strMacro = strMacro.Replace("%hour%", Convert.ToString(time.Hour));

            // 时 h2
            strMacro = strMacro.Replace("%h2%", Convert.ToString(time.Hour).PadLeft(2, '0'));

            // 分 minute
            strMacro = strMacro.Replace("%minute%", Convert.ToString(time.Minute));

            // 分 min2
            strMacro = strMacro.Replace("%min2%", Convert.ToString(time.Minute).PadLeft(2, '0'));

            // 秒 second
            strMacro = strMacro.Replace("%second%", Convert.ToString(time.Second));

            // 秒 sec2
            strMacro = strMacro.Replace("%sec2%", Convert.ToString(time.Second).PadLeft(2, '0'));

            // 百分秒 hsec
            strMacro = strMacro.Replace("%hsec%", Convert.ToString(time.Millisecond / 100));

            // 毫秒 msec
            strMacro = strMacro.Replace("%msec%", Convert.ToString(time.Millisecond));


            return strMacro;
        }

        #endregion

        public string GetMarc()
        {
            MarcRecord record = new MarcRecord(this._header);

            MarcField current_field_node = null;
            foreach (EasyLine line in this.Items)
            {
                if (line is FieldLine)
                {
                    FieldLine field = line as FieldLine;

                    MarcField field_node = null;
                    
                    if (field.IsControlField == true)
                        field_node = new MarcField(field.Name, "", field.Content);
                    else
                        field_node = new MarcField(field.Name, field.Indicator, "");

                    record.ChildNodes.add(field_node);
                    current_field_node = field_node;
                }
                else if (line is SubfieldLine)
                {
                    SubfieldLine subfield = line as SubfieldLine;

                    MarcSubfield subfield_node = new MarcSubfield(subfield.Name, subfield.Content);
                    current_field_node.ChildNodes.add(subfield_node);
                }
            }

            return record.Text;
        }

        void RefreshCaption()
        {
            string strCurrentFieldName = "";
            foreach (EasyLine line in this.Items)
            {
                if (line is FieldLine)
                {
                    FieldLine field = line as FieldLine;
                    line.Caption = GetCaption(line.Name, "", this._includeNumber);
                    strCurrentFieldName = line.Name;
                }
                else if (line is SubfieldLine)
                {
                    SubfieldLine subfield = line as SubfieldLine;
                    line.Caption = GetCaption(strCurrentFieldName, subfield.Name, this._includeNumber);
                }
            }
        }

        #region marcdef

        /// <summary>
        /// 获得配置文件的 XmlDocument 对象
        /// </summary>
        public event GetConfigDomEventHandle GetConfigDom = null;

        XmlDocument m_domMarcDef = null;
        string m_strMarcDomError = "";

        /// <summary>
        /// 存储了 MARC 结构定义的 XmlDocument 对象
        /// </summary>
        public XmlDocument MarcDefDom
        {
            get
            {
                if (this.m_domMarcDef != null)
                    return this.m_domMarcDef;

                if (m_strMarcDomError != "")
                    return null;    // 避免反复报错

                string strError = "";

                GetConfigDomEventArgs e = new GetConfigDomEventArgs();
                e.Path = "marcdef";
                e.XmlDocument = null;
                if (this.GetConfigDom != null)
                {
                    this.GetConfigDom(this, e);
                }
                else
                {
                    //throw new Exception("GetConfigFile事件尚未初始化");
                    return null;
                }

                if (string.IsNullOrEmpty(e.ErrorInfo) == false)
                {
                    strError = "获取marcdef dom出错，原因:" + e.ErrorInfo;
                    goto ERROR1;
                }

                this.m_domMarcDef = e.XmlDocument;
                return this.m_domMarcDef;
            ERROR1:
                m_strMarcDomError = strError;
                return null;
            }
            set
            {
                this.m_strMarcDomError = "";
                this.m_domMarcDef = value;
            }
        }

        /// <summary>
        /// 当前界面语言代码
        /// </summary>
        public string Lang = "zh";

        bool _includeNumber = false;

        public bool IncludeNumber
        {
            get
            {
                return this._includeNumber;
            }
            set
            {
                if (this._includeNumber != value)
                {
                    this._includeNumber = value;
                    // 刷新 Caption
                    this.RefreshCaption();
                }
            }
        }


        // 从配置信息中得到一个字段的指定语言版本的标签名称
        // parameters:
        //		strFieldName	字段名
        // return:
        //		如果找不到则返回原始字段名或者子字段名；找到返回具体的标签信息
        internal string GetCaption(string strFieldName,
            string strSubfieldName,
            bool bIncludeNumber)
        {
            string strDefault = "";
            if (string.IsNullOrEmpty(strSubfieldName) == false)
                strDefault = strSubfieldName;
            else
                strDefault = strFieldName;

            if (this.MarcDefDom == null)
                return strDefault;

            XmlNode nodeProperty = null;
            
            if (string.IsNullOrEmpty(strSubfieldName) == true)
                nodeProperty = this.MarcDefDom.DocumentElement.SelectSingleNode("Field[@name='" + strFieldName + "']/Property");
            else
                nodeProperty = this.MarcDefDom.DocumentElement.SelectSingleNode("Field[@name='" + strFieldName + "']/Subfield[@name='" + strSubfieldName + "']/Property");
            if (nodeProperty == null)
                return strDefault;

            // 从一个元素的下级的多个<strElementName>元素中, 提取语言符合的XmlNode的InnerText
            // parameters:
            //      bReturnFirstNode    如果找不到相关语言的，是否返回第一个<strElementName>
            string strValue = DomUtil.GetXmlLangedNodeText(
        this.Lang,
        nodeProperty,
        "Label",
        true);
            if (String.IsNullOrEmpty(strValue) == true)
                strValue = strDefault;

            if (bIncludeNumber == false)
                return strValue;

            if (string.IsNullOrEmpty(strSubfieldName) == false)
                return strSubfieldName + " " + strValue;
            return strFieldName + " " + strValue;
        }

        #endregion

        int m_nInSuspend = 0;

        public void DisableUpdate()
        {
            if (this.m_nInSuspend == 0)
            {
                this.tableLayoutPanel_content.SuspendLayout();
            }

            this.m_nInSuspend++;
        }

        // parameters:
        //      bOldVisible 如果为true, 表示真的要结束
        public void EnableUpdate()
        {
            this.m_nInSuspend--;

            if (this.m_nInSuspend == 0)
            {
                this.tableLayoutPanel_content.ResumeLayout(false);
                this.tableLayoutPanel_content.PerformLayout();
            }
        }

        internal static int RESERVE_LINES = 0;

        public void InsertNewLine(int index,
            EasyLine line,
            bool bFireEnvent)
        {
            this.DisableUpdate();   // 防止闪动

            try
            {
                RowStyle style = new RowStyle();
                //style.Height = 26;
                //style.SizeType = SizeType.Absolute;

                this.tableLayoutPanel_content.RowStyles.Insert(index + RESERVE_LINES, style);
                this.tableLayoutPanel_content.RowCount += 1;

                line.InsertToTable(this.tableLayoutPanel_content, index);

                this.Items.Insert(index, line);

                line.State = ItemState.New;

                if (bFireEnvent == true)
                    this.FireTextChanged();
            }
            finally
            {
                this.EnableUpdate();
            }
        }

        private void EasyMarcControl_SizeChanged(object sender, EventArgs e)
        {
            this.tableLayoutPanel_content.Width = this.Width - SystemInformation.VerticalScrollBarWidth;
        }

        private void tableLayoutPanel_content_Paint(object sender, PaintEventArgs e)
        {
            Brush brush = new SolidBrush(Color.FromArgb(230, 230, 230));
            Brush brushSubfield = new SolidBrush(Color.FromArgb(240, 240, 240));

            LinearGradientBrush brushGradient = null;
#if NO
            int nLineLength = (int)(this.tableLayoutPanel_content.ColumnStyles[0].Width
                + this.tableLayoutPanel_content.ColumnStyles[1].Width);
#endif
            int nLineLength = 0;

            using (Pen pen = new Pen(Color.FromArgb(225,225,225)))
            {

                Point p = this.tableLayoutPanel_content.PointToScreen(new Point(0, 0));

                // float y = row_heights[0];   // +this.AutoScrollPosition.Y + this.tableLayoutPanel_content.Location.Y;
                for (int i = 0; i < this.Items.Count; i++)
                {
                    EasyLine item = this.Items[i];


                    if (item.Visible == true)
                    {
                    if (nLineLength == 0)
                        nLineLength = item.textBox_content.Location.X - item.textBox_content.Margin.Left;



                        Rectangle rect = item.label_color.RectangleToScreen(item.label_color.ClientRectangle);
                        rect.Width = nLineLength;   //  this.tableLayoutPanel_content.DisplayRectangle.Width;
                        rect.Offset(-p.X, -p.Y);
                        // rect.Height = (int)this.Font.GetHeight() + 8;


                        if (item is FieldLine)
                        {

                            if (item.ExpandState != ExpandState.None)
                            {
                                if (brushGradient == null)
                                    brushGradient = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(nLineLength, 0),
                Color.FromArgb(230, 230, 230),
                Color.FromArgb(255, 255, 255)
                );
                                e.Graphics.FillRectangle(brushGradient, rect);
                            }
                            else
                                e.Graphics.FillRectangle(brush, rect);

                            Point pt1 = new Point(rect.X, rect.Y);
                            Point pt2 = new Point(rect.X + rect.Width, rect.Y);

                            e.Graphics.DrawLine(pen, pt1, pt2);

                        }
                        else
                            e.Graphics.FillRectangle(brushSubfield, rect);

                    }
                    // y += height;
                }
            }

        }

        internal bool m_bFocused = false;

        private void EasyMarcControl_Enter(object sender, EventArgs e)
        {
            this.tableLayoutPanel_content.Focus();
            this.m_bFocused = true;
            this.RefreshLineColor();

        }

        private void EasyMarcControl_Leave(object sender, EventArgs e)
        {
            this.m_bFocused = false;
            this.RefreshLineColor();

        }

    }

    // 字段行
    public class FieldLine : EasyLine
    {
        public FieldLine(EasyMarcControl container) : base(container)
        {
            // this.textBox_content.ReadOnly = true;

            this.label_caption.Font = new Font(this.Container.Font, FontStyle.Bold);
            this.label_caption.ForeColor = Color.DarkGreen;

            this.textBox_content.KeyPress -= new KeyPressEventHandler(textBox_content_KeyPress);
            this.textBox_content.KeyPress += new KeyPressEventHandler(textBox_content_KeyPress);

            this.textBox_content.KeyDown -= new KeyEventHandler(textBox_content_KeyDown);
            this.textBox_content.KeyDown += new KeyEventHandler(textBox_content_KeyDown);
        }


        bool _isControlField = false;
        public bool IsControlField
        {
            get
            {
                return _isControlField;
            }
            set
            {
                _isControlField = value;
                // this.textBox_content.ReadOnly = !value;
                if (value == true)
                {
                    this.textBox_content.BorderStyle = BorderStyle.None;
                    this.textBox_content.Dock = DockStyle.Fill;
                    this.textBox_content.MaxLength = 0;
                    this._bOverwrite = false;
                    this.textBox_content.Font = this.Container.Font;

                    textBox_content.MinimumSize = new Size(80, 21); // 23
                    textBox_content.Size = new Size(80, 21); // 23

                    this.textBox_content.Visible = true;
                }
                else
                {
                    this.textBox_content.BorderStyle = BorderStyle.FixedSingle;
                    this.textBox_content.Dock = DockStyle.Left;
                    this.textBox_content.MaxLength = 2;
                    this._bOverwrite = true;
                    this.textBox_content.Font = this.Container.FixedFont;

                    textBox_content.MinimumSize = new Size(20, 21); // 23
                    textBox_content.Size = new Size(20, 21); // 23

                    this.textBox_content.Visible = !this.Container.HideIndicator;
                }
            }
        }

        public string Indicator
        {
            get
            {
                if (_isControlField == true)
                    throw new Exception("控制字段不能获取 Indicator");
                return this.textBox_content.Text;
            }
            set
            {
                if (_isControlField == true)
                    throw new Exception("控制字段不能设置 Indicator");

                this.textBox_content.Text = value;
            }
        }

        bool _bOverwrite = false;

        void textBox_content_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case (char)Keys.Back:
                    {
                        if (this._bOverwrite == true)
                        {
                            e.Handled = true;
                            Console.Beep();
                            return;
                        }
                    }
                    break;
                default:
                    {
                        if (this._bOverwrite == true)
                        {
                            if ((Control.ModifierKeys == Keys.Control)
                                // || Control.ModifierKeys == Keys.Shift
                                || Control.ModifierKeys == Keys.Alt)
                            {
                                break;
                            }
                            int nOldSelectionStart = this.textBox_content.SelectionStart;
                            if (nOldSelectionStart < this.textBox_content.Text.Length)
                            {
                                if (this.textBox_content.Text.Length >= this.textBox_content.MaxLength) // 2009/3/6 changed
                                {
                                    this.textBox_content.Text = this.textBox_content.Text.Remove(this.textBox_content.SelectionStart, 1 + (this.textBox_content.Text.Length - this.textBox_content.MaxLength));
                                    this.textBox_content.SelectionStart = nOldSelectionStart;
                                }

                            }
                            else
                            {
                                Console.Beep(); // 表示拒绝了输入的字符
                            }
                        }
                    }
                    break;
            }

        }

        void textBox_content_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Delete:
                    {
                        if (this._bOverwrite == true)
                        {
                            // 在 字段名 或 指示符 位置
                            int nStart = this.textBox_content.SelectionStart;
                            if (nStart < this.textBox_content.MaxLength)
                            {
                                this.textBox_content.Text = this.textBox_content.Text.Substring(0, nStart) + " " + this.textBox_content.Text.Substring(nStart + 1);
                                this.textBox_content.SelectionStart = nStart;
                                e.Handled = true;
                            }
                            return;
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public override bool Visible
        {
            get
            {
                return base.Visible;
            }
            set
            {
                this.label_color.Visible = value;
                this.label_caption.Visible = value;
                if (this.splitter != null)
                    this.splitter.Visible = value;
                if (this.Container.HideIndicator == false
                    || this._isControlField == true
                    )
                    this.textBox_content.Visible = value;
            }
        }
    }

    // 子字段行
    public class SubfieldLine : EasyLine
    {
        public SubfieldLine(EasyMarcControl container)
            : base(container)
        {
            this.label_caption.TextAlign = ContentAlignment.MiddleRight;
            // this.label_caption.BackColor = SystemColors.Window;
            this.label_caption.ForeColor = SystemColors.GrayText;
        }
    }

#if NO
    // 指示符行
    public class IndicatorLine : EasyLine
    {
        public IndicatorLine(EasyMarcControl container)
            : base(container)
        {
            this.label_caption.TextAlign = ContentAlignment.MiddleRight;
        }
    }
#endif

    public enum ExpandState
    {
        None = 0,
        Expanded = 1,
        Collapsed = 2,
    }

    /// <summary>
    /// 视觉行基类
    /// </summary>
    public class EasyLine
    {
        public EasyMarcControl Container = null;

        public object Tag = null;   // 用于存放需要连接的任意类型对象

        // 颜色、popupmenu
        public Label label_color = null;

        public Label label_caption = null;

        public Splitter splitter = null;

        public TextBox textBox_content = null;

        ItemState m_state = ItemState.Normal;

        // 字段名或者子字段名
        string _name = "";
        public string Name
        {
            get
            {
                return this._name;
            }
            set
            {
                this._name = value;
            }
        }

        public ExpandState ExpandState
        {
            get
            {
                if (this.label_color.ImageIndex == -1)
                    return EasyMarc.ExpandState.None;
                if (this.label_color.ImageIndex == 0)
                    return EasyMarc.ExpandState.Collapsed;
                return EasyMarc.ExpandState.Expanded;
            }
            set
            {
                if (value == EasyMarc.ExpandState.None)
                    this.label_color.ImageIndex = -1;
                else if (value == EasyMarc.ExpandState.Collapsed)
                    this.label_color.ImageIndex = 0;
                else
                    this.label_color.ImageIndex = 1;
            }
        }



        public EasyLine(EasyMarcControl container)
        {

            this.Container = container;
            // int nTopBlank = (int)this.Container.Font.GetHeight() + 2;

            label_color = new Label();
            label_color.Dock = DockStyle.Fill;
            label_color.Size = new Size(6, 23);
            label_color.Margin = new Padding(0, 0, 0, 0);

            label_color.ImageList = this.Container.ImageListIcons;
            label_color.ImageIndex = -1;

            label_caption = new Label();
            label_caption.Dock = DockStyle.Fill;
            label_caption.Size = new Size(6, 23);
            label_caption.AutoSize = true;
            label_caption.Margin = new Padding(4, 2, 4, 0);
            // label_caption.BackColor = SystemColors.Control;

            splitter = new TransparentSplitter();
            // splitter.Dock = DockStyle.Fill;
            splitter.Size = new Size(8, 23);
            splitter.Width = 8;
            splitter.Margin = new Padding(0, 0, 0, 0);
            splitter.BackColor = Color.Transparent;

            // 字段/子字段内容
            this.textBox_content = new TextBox();
            textBox_content.BorderStyle = BorderStyle.None;
            textBox_content.Dock = DockStyle.Fill;
            textBox_content.MinimumSize = new Size(20, 21); // 23
            textBox_content.Size = new Size(20, 21); // 23
            // textBox_price.Multiline = true;
            textBox_content.Margin = new Padding(8, 4, 0, 0);
            // textBox_content.BackColor = Color.Red;
        }

        // 从tablelayoutpanel中移除本Item涉及的控件
        // parameters:
        //      nRow    从0开始计数
        internal void RemoveFromTable(TableLayoutPanel table,
            int nRow)
        {
            this.Container.DisableUpdate();

            try
            {
                // 移除本行相关的控件
                table.Controls.Remove(this.label_color);
                table.Controls.Remove(this.label_caption);
                if (this.splitter != null)
                    table.Controls.Remove(this.splitter);
                table.Controls.Remove(this.textBox_content);

                Debug.Assert(this.Container.Items.Count == table.RowCount - 2, "");

                // 然后压缩后方的
                int nEnd = Math.Min(table.RowCount - 1 - 1, this.Container.Items.Count - 1);
                for (int i = nRow; i < nEnd; i++)
                {
                    for (int j = 0; j < table.ColumnStyles.Count; j++)
                    {
                        Debug.Assert(i + EasyMarcControl.RESERVE_LINES + 1 < table.RowStyles.Count, "");

                        Control control = table.GetControlFromPosition(j, i + EasyMarcControl.RESERVE_LINES + 1);
                        if (control != null)
                        {
                            table.Controls.Remove(control);
                            table.Controls.Add(control, j, i + EasyMarcControl.RESERVE_LINES);
                        }
                    }

                }

                table.RowCount--;
                table.RowStyles.RemoveAt(nRow);
            }
            finally
            {
                this.Container.EnableUpdate();
            }
        }

        // 插入本Line到某行。调用前，table.RowCount已经增量
        // parameters:
        //      nRow    从0开始计数
        internal void InsertToTable(TableLayoutPanel table,
            int nRow)
        {
            this.Container.DisableUpdate();

            try
            {

                Debug.Assert(table.RowCount == this.Container.Items.Count + 3, "");

                // 先移动后方的
                int nEnd = Math.Min(table.RowCount - 1 - 1, this.Container.Items.Count - 1);
                for (int i = nEnd; i >= nRow; i--)
                {
                    // EasyLine line = this.Container.Items[i];

                    for (int j = 0; j < table.ColumnStyles.Count; j++)
                    {
                        Debug.Assert(i + EasyMarcControl.RESERVE_LINES + 1 < table.RowStyles.Count, "");

                        Control control = table.GetControlFromPosition(j, i + EasyMarcControl.RESERVE_LINES);
                        if (control != null)
                        {
                            table.Controls.Remove(control);
                            table.Controls.Add(control, j, i + EasyMarcControl.RESERVE_LINES + 1);
                        }
                    }

#if NO
                    // color
                    Label label = line.label_color;
                    table.Controls.Remove(label);
                    table.Controls.Add(label, 0, i + EasyMarcControl.RESERVE_LINES + 1);

                    // fieldname
                    Label fieldName = line.label_caption;
                    table.Controls.Remove(fieldName);
                    table.Controls.Add(fieldName, 1, i + 1 + 1);

                    // subfield content
                    TextBox subfieldContent = line.textBox_content;
                    table.Controls.Remove(subfieldContent);
                    table.Controls.Add(subfieldContent, 2, i + 1 + 1);
#endif
                }

                table.Controls.Add(this.label_color, 0, nRow + EasyMarcControl.RESERVE_LINES);
                table.Controls.Add(this.label_caption, 1, nRow + EasyMarcControl.RESERVE_LINES);
                if (this.splitter != null)
                    table.Controls.Add(this.splitter, 2, nRow + EasyMarcControl.RESERVE_LINES);
                table.Controls.Add(this.textBox_content, 3, nRow + EasyMarcControl.RESERVE_LINES);
            }
            finally
            {
                this.Container.EnableUpdate();
            }

            // events
            AddEvents();
        }


        void AddEvents()
        {
            this.label_caption.MouseUp -= new MouseEventHandler(label_color_MouseUp);
            this.label_caption.MouseUp += new MouseEventHandler(label_color_MouseUp);
            this.label_color.MouseUp -= new MouseEventHandler(label_color_MouseUp);
            this.label_color.MouseUp += new MouseEventHandler(label_color_MouseUp);

            this.label_caption.MouseClick -= new MouseEventHandler(label_caption_MouseClick);
            this.label_caption.MouseClick += new MouseEventHandler(label_caption_MouseClick);
            this.label_color.MouseClick -= new MouseEventHandler(label_color_MouseClick);
            this.label_color.MouseClick += new MouseEventHandler(label_color_MouseClick);

            this.textBox_content.TextChanged -= new EventHandler(textBox_content_TextChanged);
            this.textBox_content.TextChanged += new EventHandler(textBox_content_TextChanged);

            this.textBox_content.Enter -= new EventHandler(control_Enter);
            this.textBox_content.Enter += new EventHandler(control_Enter);

            // this.splitter.Paint += new PaintEventHandler(splitter_Paint);

            this.splitter.MouseDown -= new MouseEventHandler(splitter_MouseDown);
            this.splitter.MouseDown += new MouseEventHandler(splitter_MouseDown);

            this.splitter.MouseUp -= new MouseEventHandler(splitter_MouseUp);
            this.splitter.MouseUp += new MouseEventHandler(splitter_MouseUp);

#if NO
            this.label_color.MouseWheel -= new MouseEventHandler(textBox_comment_MouseWheel);
            this.label_color.MouseWheel += new MouseEventHandler(textBox_comment_MouseWheel);

            this.label_caption.MouseWheel -= new MouseEventHandler(textBox_comment_MouseWheel);
            this.label_caption.MouseWheel += new MouseEventHandler(textBox_comment_MouseWheel);
#endif
        }

        void splitter_MouseUp(object sender, MouseEventArgs e)
        {
            int nDelta = e.X - _nSplitterStart;
            this.Container.ChangeCaptionWidth(nDelta);
        }

        int _nSplitterStart = 0;
        void splitter_MouseDown(object sender, MouseEventArgs e)
        {
            _nSplitterStart = e.X;
        }

#if NO
        void textBox_comment_MouseWheel(object sender, MouseEventArgs e)
        {
            TableLayoutPanel table = this.Container.TableLayoutPanel;

            int nValue = table.VerticalScroll.Value;
            nValue -= e.Delta;
            if (nValue > table.VerticalScroll.Maximum)
                nValue = table.VerticalScroll.Maximum;
            if (nValue < table.VerticalScroll.Minimum)
                nValue = table.VerticalScroll.Minimum;

            if (table.VerticalScroll.Value != nValue)
            {
                table.VerticalScroll.Value = nValue;
                table.PerformLayout();
            }
        }
#endif

        void control_Enter(object sender, EventArgs e)
        {
            this.Container.SelectItem(this, true);
        }

        void textBox_content_TextChanged(object sender, EventArgs e)
        {
            if ((this.State & ItemState.New) == 0)
                this.State |= ItemState.Changed;

            // this.Container.Changed = true;
            this.Container.FireTextChanged();
        }

        void label_color_MouseClick(object sender, MouseEventArgs e)
        {
            this.Container.m_bFocused = true;
            this.Container.TableLayoutPanel.Focus();

            if (e.Button == MouseButtons.Left)
            {
                if (Control.ModifierKeys == Keys.Control)
                {
                    this.Container.ToggleSelectItem(this);
                }
                else if (Control.ModifierKeys == Keys.Shift)
                    this.Container.RangeSelectItem(this);
                else
                {
                    this.Container.SelectItem(this, true);
                }

                if (this.ExpandState != EasyMarc.ExpandState.None)
                {
                    this.Container.ToggleExpand(this);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 如果当前有多重选择，则不必作什么l
                // 如果当前为单独一个选择或者0个选择，则选择当前对象
                // 这样做的目的是方便操作
                if (this.Container.SelectedIndices.Count < 2)
                {
                    this.Container.SelectItem(this, true);
                }
            }

            // 2014/9/30
            // this.EnsureVisible();
        }

        void label_caption_MouseClick(object sender, MouseEventArgs e)
        {
            this.Container.m_bFocused = true;
            this.Container.TableLayoutPanel.Focus();

            if (e.Button == MouseButtons.Left)
            {
                if (Control.ModifierKeys == Keys.Control)
                {
                    this.Container.ToggleSelectItem(this);
                }
                else if (Control.ModifierKeys == Keys.Shift)
                    this.Container.RangeSelectItem(this);
                else
                {
                    this.Container.SelectItem(this, true);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // 如果当前有多重选择，则不必作什么l
                // 如果当前为单独一个选择或者0个选择，则选择当前对象
                // 这样做的目的是方便操作
                if (this.Container.SelectedIndices.Count < 2)
                {
                    this.Container.SelectItem(this, true);
                }
            }

            // 2014/9/30
            this.EnsureVisible();
        }

        void label_color_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            ContextMenu contextMenu = new ContextMenu();
            MenuItem menuItem = null;

            int nSelectedCount = this.Container.SelectedIndices.Count;

            EasyLine first_line = null;
            if (nSelectedCount > 0)
                first_line = this.Container.SelectedItems[0];

            //
            menuItem = new MenuItem("新增字段(&F)");
            menuItem.Click += new System.EventHandler(this.menu_newField_Click);
            contextMenu.MenuItems.Add(menuItem);

            //
            menuItem = new MenuItem("新增子字段(&S)");
            menuItem.Click += new System.EventHandler(this.menu_newSubfield_Click);
            if (first_line != null
                && first_line is FieldLine
                && (first_line as FieldLine).IsControlField == true)    // 控制字段下，无法插入子字段
                menuItem.Enabled = false;
            contextMenu.MenuItems.Add(menuItem);

            // ---
            menuItem = new MenuItem("-");
            contextMenu.MenuItems.Add(menuItem);


            //
            menuItem = new MenuItem("删除(&D)");
            menuItem.Click += new System.EventHandler(this.menu_deleteElements_Click);
            contextMenu.MenuItems.Add(menuItem);

            // ---
            menuItem = new MenuItem("-");
            contextMenu.MenuItems.Add(menuItem);

            //
            menuItem = new MenuItem("全部收缩(&C)");
            menuItem.Click += new System.EventHandler(this.menu_collapseAll_Click);
            contextMenu.MenuItems.Add(menuItem);

            //
            menuItem = new MenuItem("全部展开(&E)");
            menuItem.Click += new System.EventHandler(this.menu_expandAll_Click);
            contextMenu.MenuItems.Add(menuItem);

            // ---
            menuItem = new MenuItem("-");
            contextMenu.MenuItems.Add(menuItem);

            //
            menuItem = new MenuItem("隐藏字段(&H)");
            menuItem.Click += new System.EventHandler(this.menu_hideField_Click);
            if (!(this is FieldLine))
                menuItem.Enabled = false;
            contextMenu.MenuItems.Add(menuItem);

            int nHideCount = this.Container.GetHideFieldCount();
            //
            menuItem = new MenuItem("恢复显示隐藏的字段 [" + nHideCount.ToString()+ "] (&E)");
            menuItem.Click += new System.EventHandler(this.menu_unHideAllFields_Click);
            if (nHideCount == 0)
                menuItem.Enabled = false;
            contextMenu.MenuItems.Add(menuItem);
            // ---
            menuItem = new MenuItem("-");
            contextMenu.MenuItems.Add(menuItem);

            //
            menuItem = new MenuItem("显示字段指示符(&I)");
            menuItem.Click += new System.EventHandler(this.menu_toggleDisplayIndicator_Click);
            menuItem.Checked = !this.Container.HideIndicator;
            contextMenu.MenuItems.Add(menuItem);

            //
            menuItem = new MenuItem("显示原始字段名、子字段名(&N)");
            menuItem.Click += new System.EventHandler(this.menu_toggleDisplayNumberName_Click);
            menuItem.Checked = this.Container.IncludeNumber;
            contextMenu.MenuItems.Add(menuItem);


            /*
            menuItem = new MenuItem("test");
            menuItem.Click += new System.EventHandler(this.menu_test_Click);
            contextMenu.MenuItems.Add(menuItem);
             * */
            contextMenu.Show(this.label_color, new Point(e.X, e.Y));
        }

        void menu_hideField_Click(object sender, EventArgs e)
        {
            this.Container.ToggleHide(this as FieldLine);
        }

        void menu_unHideAllFields_Click(object sender, EventArgs e)
        {
            this.Container.HideFields(null, false);
        }

        void menu_collapseAll_Click(object sender, EventArgs e)
        {
            this.Container.ExpandAll(false);
        }

        void menu_expandAll_Click(object sender, EventArgs e)
        {
            this.Container.ExpandAll(true);

        }

        // 显示字段指示符
        void menu_toggleDisplayIndicator_Click(object sender, EventArgs e)
        {
            this.Container.HideIndicator = !this.Container.HideIndicator;
        }

        // 显示原始字段名、子字段名
        void menu_toggleDisplayNumberName_Click(object sender, EventArgs e)
        {
            this.Container.IncludeNumber = !this.Container.IncludeNumber;
        }

        void menu_newField_Click(object sender, EventArgs e)
        {
            int nPos = this.Container.Items.IndexOf(this);

            if (nPos == -1)
                throw new Exception("not found myself");

            FieldLine field = this.Container.NewField(nPos);
            if (field != null)
            {
                // 置于可见范围
            }
        }

        void menu_newSubfield_Click(object sender, EventArgs e)
        {
            int nPos = this.Container.Items.IndexOf(this);
            if (nPos == -1)
            {
                throw new Exception("not found myself");
            }
            SubfieldLine subfield = this.Container.NewSubfield(nPos);
            if (subfield != null)
            {
                // 置于可见范围
            }
        }

        // 删除当前元素
        void menu_deleteElements_Click(object sender, EventArgs e)
        {
            List<EasyLine> selected_lines = this.Container.SelectedItems;

            if (selected_lines.Count == 0)
            {
                MessageBox.Show(this.Container, "尚未选定要删除的事项");
                return;
            }
            string strText = "";

            if (selected_lines.Count == 1)
                strText = "确实要删除事项 '" + selected_lines[0].Caption + "'? ";
            else
                strText = "确实要删除所选定的 " + selected_lines.Count.ToString() + " 个事项?";

            DialogResult result = MessageBox.Show(this.Container,
                strText,
                "EasyMarcControl",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return;
            }

#if NO
            int nNotDeleteCount = 0;
            this.Container.DisableUpdate();
            try
            {
                for (int i = 0; i < selected_lines.Count; i++)
                {
                    EasyLine item = selected_lines[i];
                    if ((item.State & ItemState.ReadOnly) != 0)
                    {
                        nNotDeleteCount++;
                        continue;
                    }
                    this.Container.RemoveItem(item);
                }
            }
            finally
            {
                this.Container.EnableUpdate();
            }

            if (nNotDeleteCount > 0)
                MessageBox.Show(this.Container, "有 " + nNotDeleteCount.ToString() + " 项已订购状态的事项未能删除");
#endif
            this.Container.DisableUpdate();
            try
            {
                this.Container.DeleteElements(selected_lines);
            }
            finally
            {
                this.Container.EnableUpdate();
            }
        }


        // 事项状态
        public ItemState State
        {
            get
            {
                return this.m_state;
            }
            set
            {
                if (this.m_state != value)
                {
                    this.m_state = value;

                    SetLineColor();

                    bool bOldReadOnly = this.ReadOnly;
                    if ((this.m_state & ItemState.ReadOnly) != 0)
                    {
                        this.ReadOnly = true;
                    }
                    else
                    {
                        this.ReadOnly = false;
                    }
                }
            }
        }

        // 设置事项左端label的颜色
        internal void SetLineColor()
        {
            if ((this.m_state & ItemState.Selected) != 0)
            {
                // 没有焦点，又需要隐藏selection情形
                if (this.Container.HideSelection == true
                    && this.Container.m_bFocused == false)
                {
                    // 继续向后走，显示其他颜色
                }
                else
                {
                    this.label_color.BackColor = SystemColors.Highlight;
                    return;
                }
            }
            if ((this.m_state & ItemState.New) != 0)
            {
                this.label_color.BackColor = Color.Yellow;
                return;
            }
            if ((this.m_state & ItemState.Changed) != 0)
            {
                this.label_color.BackColor = Color.LightGreen;
                return;
            }
            if ((this.m_state & ItemState.ReadOnly) != 0)
            {
                this.label_color.BackColor = Color.LightGray;
                return;
            }

            this.label_color.BackColor = SystemColors.Window;
        }


        bool m_bReadOnly = false;

        public bool ReadOnly
        {
            get
            {
                return this.m_bReadOnly;
            }
            set
            {
                bool bOldValue = this.m_bReadOnly;
                if (bOldValue != value)
                {
                    this.m_bReadOnly = value;

                    // 
                    this.textBox_content.ReadOnly = value;
                }
            }
        }

        public string Content
        {
            get
            {
                return this.textBox_content.Text;
            }
            set
            {
                this.textBox_content.Text = value;
            }
        }

        public string Caption
        {
            get
            {
                return this.label_caption.Text;
            }
            set
            {
                this.label_caption.Text = value;
            }
        }

        public virtual bool Visible
        {
            get
            {
                return this.label_color.Visible;
            }
            set
            {
                this.label_color.Visible = value;
                this.label_caption.Visible = value;
                if (this.splitter != null)
                    this.splitter.Visible = value;
                this.textBox_content.Visible = value;
            }
        }

        public void EnsureVisible()
        {
            this.Container.EnsureVisible(this);
        }


    }
    // 支持透明背景色的 Splitter
    public class TransparentSplitter : Splitter
    {
        public TransparentSplitter()
            : base()
        {
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }
    }
}