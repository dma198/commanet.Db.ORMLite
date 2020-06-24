using System;

namespace commanet.Db
{
    [AttributeUsage(AttributeTargets.Property)]
    public class PDBIgnoreAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class TableNameAttribute : Attribute
    {
        public string TableName { get; set; }
        public TableNameAttribute(string Name) { TableName = Name; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class PostTableCreateScriptAttribute : Attribute
    {
        public string Script { get; set; }
        public PostTableCreateScriptAttribute(string script) { Script = script; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnDefAttribute : Attribute
    {
        public string ColumnDef { get; set; }
        public ColumnDefAttribute(string Def) { ColumnDef = Def; }
    }


}
