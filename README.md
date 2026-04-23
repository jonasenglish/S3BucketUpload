#S3 Bucket Upload

This application allows users to store, view, download, and delete images hosted on an AWS S3 Bucket. Uploading, processing, retrieving, and deleting images are all handled by AWS Lambda functions by utilizing presigned URLs to make requests.
The application uses the Unity engine as the frontend as its very fast for me to setup and develop in. Images can be selected for deletion by clicking on them. Images are verified against a SHA256 Checksum in the delete lambda to ensure that the image exists in the bucket before deletion. Images are managed per account, and accounts are setup via AWS Cognito User Pools.

For more details on the AWS Stack, view this [Git Project](https://github.com/jonasenglish/AWS-Image-Storage-CDK-Stack)

<img width="959" height="599" alt="image" src="https://github.com/user-attachments/assets/fa2e21c3-9f75-4004-8b4c-70d272966b25" />

