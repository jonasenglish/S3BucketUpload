This application allows users to store, view, and delete images hosted on an AWS S3 Bucket. Uploading, processing, retrieving, and deleting images are all handled by AWS Lambda functions by utilizing presigned URLs to make requests.
The application uses the Unity engine as a backend as its very fast for me to setup and develop in. Images can be selected for deletion by clicking on them. Images are verified against a SHA256 Checksum in the delete lambda to ensure that the image exists in the bucket before deletion.
The application can be downloaded [HERE](https://github.com/jonasenglish/S3BucketUpload/releases/tag/Release)
<img width="797" height="599" alt="image" src="https://github.com/user-attachments/assets/a503ac2d-beab-4705-a6a6-27679e91b99e" />
