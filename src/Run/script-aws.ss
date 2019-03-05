<!--
db postgres
db.connection $AWS_RDS_POSTGRES
files s3
files.config {AccessKey:$AWS_S3_ACCESS_KEY,SecretKey:$AWS_S3_SECRET_KEY,Region:us-east-1,Bucket:rockwind}
-->

{{ end | dbTableNamesWithRowCounts | textDump({ caption: 'Tables' }) }}

{{ `select * from "Product" order by "UnitPrice" desc LIMIT 5` | dbSelect | textDump({ headerStyle:'None' }) }}

{{ contentAllRootDirectories | map => `${it.Name}/`
   | union(map(contentAllRootFiles, x => x.Name))
   | textDump({ caption: 'Root Files and Folders' }) }}

{{ find ?? '*.html' | assignTo: find }}
{{ find | contentFilesFind | map => it.VirtualPath | take(15) 
   | textDump({ caption: `Files matching: ${find}` }) }}
