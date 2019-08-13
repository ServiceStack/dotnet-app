<!--
db postgres
db.connection $AWS_RDS_POSTGRES
files s3
files.config {AccessKey:$AWS_S3_ACCESS_KEY,SecretKey:$AWS_S3_SECRET_KEY,Region:us-east-1,Bucket:rockwind}
-->

Querying AWS...

```code
dbTableNamesWithRowCounts | textDump({ caption: 'Tables' })

5 | to => limit

`Last ${limit} Orders:\n`
{{ `SELECT * FROM "Order" ORDER BY "Id" DESC ${limit.sqlLimit()}` 
  | dbSelect | map => { it.Id, it.CustomerId, it.EmployeeId, Freight: it.Freight.currency() } | textDump }}

{{ vfsContent.allRootDirectories().map(dir => `${dir.Name}/`) 
  .union(vfsContent.allRootFiles().map(file => file.Name)) | textDump({caption:'Root Files + Folders'}) }}

(ARGV.first() ?? '*.jpg') | to => pattern
`\nFirst ${limit} ${pattern} files in S3:`
vfsContent.findFiles(pattern) | take(limit) | map => it.VirtualPath | join('\n')
```