

if exists (select 1
            from  sysobjects
           where  id = object_id('DataType')
            and   type = 'U')
   drop table DataType
go


if exists (select 1
            from  sysobjects
           where  id = object_id('Product')
            and   type = 'U')
   drop table Product
go


create table DataType (
   Id bigint IDENTITY(1,1) primary key,
   [Bit] bit NOT NULL,
   [Decimal] decimal(12,2) null,
   [Integer] int null,
   [Money] money null,
   [Numeric] numeric null,
   [Smallint] smallint null,
   [Enum] nvarchar(50) null,
   [Name] nvarchar(100) NOT NULL,
 
   [Created] datetime NOT NULL,
   [CreatedBy] varchar(100) NULL,
   [Updated] datetime NOT NULL,
   [UpdatedBy] varchar(100) NULL,

   
)
go


create table Product (
   Id bigint IDENTITY(1,1) primary key,
   [ProductName]  nvarchar(50) not null,
   [SupplierId] bigint,
   [UnitPrice] decimal(12,2) null default 0,
   [Package] nvarchar(30) null,
   [IsDiscontinued] bit not null default 0,
   [ProductType] nvarchar(30) null, --grocery, electronics, livestock
   

   Created datetime NOT NULL default getutcdate(),
   CreatedBy varchar(100) NULL default 'import',
   Updated datetime NOT NULL  default getutcdate(),
   UpdatedBy varchar(100) NULL default 'import',


)
go

