<!--
db sqlserver
db.connection $AZURE_SQL_CONNECTION_STRING
files azure
files.config {ConnectionString:$AZURE_BLOB_CONNECTION_STRING,ContainerName:rockwind}
-->

{{ dbTableNamesWithRowCounts | textDump({ caption: 'Tables' }) }}

{{ `SELECT "Id", "CustomerId", "EmployeeId", "OrderDate" from "Order" ORDER BY "Id" DESC ${sqlLimit(5)}`
   | dbSelect | textDump({ caption: 'Last 5 Orders', headerStyle:'None' }) }}

{{ contentAllRootDirectories | map => `${it.Name}/` 
   | union(map(contentAllRootFiles, x => x.Name))
   | textDump({ caption: 'Root Files and Folders' }) }}

{{ find ?? '*.html' | assignTo: find }}
{{ find | contentFilesFind | map => it.VirtualPath | take(5) 
   | textDump({ caption: `Files matching: ${find}` }) }}
