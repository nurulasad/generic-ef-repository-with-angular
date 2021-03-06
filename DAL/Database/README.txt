Organizing scripts

Schema/Create/
This folder contains scripts for creating the database from scratch. The file "Tables.sql" contains
scripts to create all database tables for the current version. The file "Indexes.sql" contains scripts
to create all indexes for the current version. "current version" is whatever version developers are
currently working on. For production, get script from appropriate branch in svn.


Schema/Alter/
Alter scripts to upgrade the database from any given version to the current version. As alter scripts
can be irreversible (though in practice most of the time they're not), an alter script should not be
changed after it has been run against a test database, and CANNOT be changed after it has been run
against a production database. The only exception would be if the alter script was run against a production
database and caused major problems.
This means when fixing bugs, the database bug fix should be placed in the alter script for the current
version, not the version where the bug was introduced; because old versions of alter scripts will never
be run again.
