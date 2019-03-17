using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace ManagementPortal.Model
{
    public class CustomReportInfo
    {
        public ReportParameterId Id { get; set; }
        public string Name { get; set; }
        public ReportParameterType Datatype { get; set; }
        public bool Mandatory { get; set; }
        public string Value { get; set; }
        public CustomReportInfo() { }

        public CustomReportInfo(string name, ReportParameterType dataType, bool mandatory)
        {
            Name = name;
            Datatype = dataType;
            Mandatory = mandatory;
        }
    }

    [Serializable]
    public class ReportParameterId
    {
        public ReportParameterId() { }
    }

    [Serializable]
    public enum ReportParameterType
    {
        Id,
        DateTime,
        Date,
        String,
        Decimal,
        Boolean
    }

    [Serializable]
    public class SystemReport
    {
        [XmlElement]
        public int Id { get; set; }

        [XmlElement]
        public string Name { get; set; }

        [XmlElement]
        public string Description { get; set; }

        [XmlElement]
        public string SqlTemplate { get; set; }

        [XmlElement]
        public ReportParameters ReportParameters { get; set; }

        public SystemReport()
        {
            ReportParameters = new ReportParameters();
        }
    }

    [Serializable]
    public class ReportParameters
    {
        [XmlElement]
        public List<ReportParameter> ReportParameter { get; set; }

        public ReportParameters()
        {
            ReportParameter = new List<Model.ReportParameter>();
        }
    }

    [Serializable]
    public class ReportParameter
    {
        [XmlElement]
        public long Id { get; set; }
        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Description { get; set; }
        [XmlElement]
        public string DataType { get; set; }

        [XmlElement]
        public string Precision { get; set; }

        [XmlElement]
        public bool Mandatory { get; set; }
        public string SystemInputParameter { get; set; }

        public ReportParameter() { }

        public ReportParameter(long id, string name, string description, string dataType, string precision, bool mandatory, string systemInputParameter) : this()
        {
            Id = id;
            Name = name;
            Description = description;
            DataType = dataType;
            Precision = precision;
            Mandatory = mandatory;
            SystemInputParameter = systemInputParameter;
        }
    }

    [XmlRoot]
    [Serializable]
    public class SystemReports
    {
        [XmlElement]
        public List<SystemReport> SystemReport;

        public SystemReports() { }
    }
}
