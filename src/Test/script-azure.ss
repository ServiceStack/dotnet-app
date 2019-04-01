<!--
db sqlserver
db.connection $AZURE_SQL_CONNECTION_STRING
files azure
files.config {ConnectionString:$AZURE_BLOB_CONNECTION_STRING,ContainerName:rockwind}
-->

{{ dbTableNamesWithRowCounts | textDump({ caption: 'Tables' }) }}
