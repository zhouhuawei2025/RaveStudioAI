using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaveStudioAI.Sds.Models
{
    internal class Field
    {
       
        public string FormOID { get; set; } = string.Empty;
        public string FieldOID { get; set; } = string.Empty;
        public int Oridinal { get; set; }
        public string DraftFieldName { get; set; } = string.Empty;
        public bool DraftFieldActive { get; set; } = true;
        public string VariableOID { get; set; } = string.Empty;
        public string DataFormat { get; set; } = string.Empty;
        public string DataDictionaryOID { get; set; } = string.Empty;
        public string CodingDictionary {  get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        //public bool SourceDocument { get; set; }
        public bool IsGrid { get; set; } = false;
        public string DefaultValue { get; set; } = "";
        public string SASLabel { get; set; } = string.Empty;
        public string SASFormat { get; set; } = string.Empty;
        public bool IsRequired {  get; set; } = true;
        public bool QueryFutureDate { get; set; }=false;
        public bool IsVisible { get; set; } = true;
        public string AnalyteName { get; set; } = string.Empty;
        public bool IsLab { get; set; } = false; // 替代IsClinicalSignificance
        //public bool IsClinicalSignificance { get; set; } =false; 
        public bool QueryNonConformance { get; set; } =false;
        public bool DoesNotBreakSignature { get; set; } =false;

        public void UpdateField()
        {
            this.DraftFieldName = this.FieldName;
            UpdateType();
            UpdateVariableOID();
            UpdateDataFormat();
            this.DataDictionaryOID = (this.ControlType.Contains("Radio")  || this.ControlType.Contains("Drop")) ? this.FieldOID : "";
            //UpdateCodeDic();
            UpdateQueryNonConformance();
            this.SASLabel = this.FieldName;
            UpdateSASFormat();
            UpdateIsRequired();
            UpdateLabel();
            

        }

        public void UpdateType()
        {
            if (this.ControlType == "Long Text" || this.ControlType == "Long text")
                this.ControlType = "LongText";
        }
        public void UpdateVariableOID()
        {
            if (this.ControlType == "Dynamic SearchList" && this.FieldOID.Contains("AE"))
            {
                this.VariableOID = "AELINK";
            }
            else if (this.ControlType == "Dynamic SearchList" && this.FieldOID.Contains("MH"))
            {
                this.VariableOID = "MHLINK";
            }
            else
            {
                this.VariableOID = this.FieldOID;
            }
        }

        public void UpdateDataFormat()
        {
            switch(this.ControlType)
            {
                case "RadioButton":
                case "RadioButton (Vertical)":
                case "DropDownList":
                    this.DataFormat = "2";
                    break;
                case "CheckBox":
                    this.DataFormat = "1";
                    break;
                case "Dynamic SearchList":
                    this.DataFormat = "$100";
                    break;
                case "DateTime":
                    if (this.DataFormat.Replace("-", "") == "dd MMM yyyy"
                        || this.DataFormat.Replace("-", "") == "HH:nn"
                        || this.DataFormat.Replace("-", "") == "HH:nn:ss"
                        || this.DataFormat.Replace("-", "") == "dd MMM yyyy HH:nn:ss")
                        this.DataFormat = this.DataFormat;
                    else
                        this.DataFormat = "dd MMM yyyy";
                    break;
                case "LongText":
                    if (!this.DataFormat.Contains('$')) this.DataFormat = "$200";
                    break;

            }
        }

        public void UpdateCodeDic()
        {
            if(this.FieldOID.Contains("MHTERM")
            || this.FieldOID.Contains("AETERM")
            || this.FieldOID.Contains("PRTRT"))
            {
                this.CodingDictionary = "MedDRA (Coder)";
            }

            else if(this.FieldOID.Contains("ANMTRT")
                 || this.FieldOID.Contains("CMTRT"))
            {
                this.CodingDictionary = "WHODrug (Coder)";
            }
        }

        public void UpdateLabel()
        {
            if (this.ControlType == "Label")
            {
                this.ControlType = "Text";
                this.VariableOID = "";
                this.DataFormat = "";
                this.SASFormat = "";
                this.SASLabel = "";
                this.IsRequired = false;
            }
        }

        public void UpdateSASFormat()
        {
            switch (this.ControlType)
            {
                case "Dynamic SearchList":
                    this.SASFormat = "$300";
                    break;
                case "LongText":
                    this.SASFormat = "$600";
                    break;
                case "RadioButton":
                case "RadioButton (Vertical)":
                case "DropDownList":
                case "CheckBox":
                    this.SASFormat = this.DataFormat;
                    break;
                case "Text":
                    this.SASFormat = this.DataFormat.Replace("+", "").Trim();
                    break;
                case "DateTime":
                    this.SASFormat = "";
                    break;
            }


        }

        public void UpdateIsRequired()
        {
            if (this.ControlType == "CheckBox" || this.ControlType == "Dynamic SearchList")
                this.IsRequired = false;
            if(this.FieldOID.Contains("COMMENT") || this.FieldName == "备注")
                this.IsRequired = false;

        }

        public void UpdateQueryNonConformance()
        {
            switch (this.ControlType)
            {
                case "Dynamic SearchList":
                case "RadioButton":
                case "RadioButton (Vertical)":
                case "DropDownList":
                case "CheckBox":
                case "Label":
                    this.QueryNonConformance = false;
                    break;
                
                case "DateTime":
                case "LongText":
                case "Text":
                    this.QueryNonConformance = true;
                    break;
            }
        }


    }
}
