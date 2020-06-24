## commanet.Db.ORMLite
--------------------------
Extension to *commanet.Db.SQL* package.
Provides lightweight object-oriented interface with SQL Databases.
Idea is to concentrate data structures definition in one place - c# code.
Mapping to DB tables and all operations with data to be performed on the base of
single place defined c# class.

This aproach is useful not everywhere. There are advantages and disadvantages in it. 

**Prepare Data Structure**
```c#
// Class Attribute TableName defines real table name used
// in ORM operations. It is optional, if not defined will used
// class name according names convention described below.  
[TableName("MY_REAL_DATABASE_TABLE_NAME")]
// Another optional Attribute defines script to be runned 
// after table created. Script is semicolon separated 
// SQL-operators where can use {TNAME} MACRO - will be filled
// by real table name. 
[PostTableCreateScript(@"
         CREATE UNIQUE INDEX {TNAME}_01 ON {TNAME}(MyCounter);
         CREATE INDEX {TNAME}_02 ON {TNAME}(MyDate);
    ")]
public class MyTableClass
{
    // Attribute ColumnDef defines text to be used in automatically
    // generated  CREATE TABLE sentense     				
    [ColumnDef("INTEGER")]  
    public long MyCounter { get; set; } = 1;
    [ColumnDef("DATE")]
    public DateTime MyDate { get; set; } = DateTime.Now;
    [ColumnDef("VARCHAR2(2000 CHAR)")]
    public string MyString { get; set; } = "";
}
```  
Names convention used to transaltion table and column names from DB and back to c#:  
All DB names supposed to be in "Snake Case", all c# names supposed to be in "Camel Case":  
Example: 

C#                 | DB
-------------------|-------
HeatId             | HEAT_ID
LadleArriveDate    | LADLE_ARRIVE_DATE

**Schema Generation**
```c#
var db = new SQLDBConnection(DbType, DbUser, DbPassword, DbConStr);
var generator = new ORMDBSchemaGenerator(db, Logger);

generator.AddTableClass<MyTableClass1>();
generator.AddTableClass<MyTableClass2>();

// Optionally can be defined script to be executed after schema generation done
generator.PostGenerationScript = @"
    CREATE USER SOME_USER IDENTIFIED BY SOME_USER;     
    GRANT CONNECT TO SOME_USER;
    GRANT SELECT ON MyTable TO SOME_USER;
    GRANT UPDATE(MY_STRING) ON MY_TABLE TO SOME_USER;
    ...
";
...
// Blow call will check if tables are exists and create them 
// if missing in DB. In current library version it does not
// check if table structure is changed - cretion will happens
// only if table is completely missing
generator.GenerateSchema(DbUser);
```

**Create ORM Helper**
```c#
var db = new SQLDBConnection(DbType, DbUser, DbPassword, DbConStr);
ORMLite odb = new ORMLite(db);
```  

**Using ORM Helper for data manipulation**

```c#
var SQL = "SELECT * FROM MY_TABLE";
var data = odb.SelectAll<MyTableClass>(SQL);
foreach(row in data)
{
    Console.WriteLine("Data: {0}",row.MyString);
}   
// Variant to read one row 
var oneRow=odb.SelectOne<MyTableClass>(SQL);
```

With Parameters:
```c#
var SQL = "SELECT * FROM MY_TABLE WHERE my_counter=:C";
var data=odb.SelectAll<MyTableClass>( // Returns list of MyTableClass objects
    SQL,
    false, // Do not generate exception if not all object properties filled from query
    DbParams.New("C",123) // Set query parameter
);

foreach(row in data)
{
    Console.WriteLine("Data: {0}",row.MyString);
}   
```
----------------------------------------------------
##### Modify data in DB.  
All data modifications same as in base *SQLDBConnection* are wrapped in *Transaction* call with special version of transaction helper provided as input parameter. 
```c#
var data = new MyTableClass()
{
    MyCounter=1,
    MyDate=DateTime.Now,
    MyString="Hello ORMLite"
}
odb.Transaction((th)=>{ 
  // INSERT Row Into Table
  th.Insert(
    data,          // Object contains data to update
    "MY_TABLE"     // Table Name to Update
  );
 
  // Update Table Row with using  Key(s) 
  th.Update(
    data,          // Object contains data to update
    "MY_TABLE",    // Table Name to Update
    "MY_COUNTER"   // Table key used for update 
  );

  // UPDATE or INSERT if missing in DB 
  th.UpdateOrInsert(
    data,          // Object contains data to update
    "MY_TABLE",    // Table Name to Update
    true,
    "MY_COUNTER"   // Table key used for update 
  );
});



```

