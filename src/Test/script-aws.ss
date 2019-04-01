<!--
db postgres
db.connection $AWS_RDS_POSTGRES
files s3
files.config {AccessKey:$AWS_S3_ACCESS_KEY,SecretKey:$AWS_S3_SECRET_KEY,Region:us-east-1,Bucket:rockwind}
-->

{{ dbTableNamesWithRowCounts | textDump({ caption: 'Tables' }) }}
