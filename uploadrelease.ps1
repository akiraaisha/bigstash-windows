try {
	$res = Invoke-Command -ScriptBlock { aws configure get aws_access_key_id } 
	if ($res) { 
		aws s3 cp . s3://bigstashstatic/apps/windows/ --recursive --acl public-read  --exclude '*.manifest' --exclude '*.application'
		aws s3 cp . s3://bigstashstatic/apps/windows/ --recursive --acl public-read --exclude '*' --include '*.manifest' --content-type application/x-ms-application
		aws s3 cp . s3://bigstashstatic/apps/windows/ --recursive --acl public-read --exclude '*' --include '*.application' --content-type application/x-ms-application
	} else {
	   echo "AWS CLI not configured. Configure with 'aws configure'"
	}
} catch {
   echo "Error running aws cli, is it installed? try running 'aws'."
   
}
