This application allows users to store, view, and delete images hosted on an AWS S3 Bucket. Uploading, processing, retrieving, and deleting images are all handled by AWS Lambda functions by utilizing presigned URLs to make requests.
The application uses the Unity engine as a backend as its very fast for me to setup and develop in. Images can be selected for deletion by clicking on them. Images are verified against a SHA256 Checksum in the delete lambda to ensure that the image exists in the bucket before deletion.
The application can be downloaded [HERE](https://github.com/jonasenglish/S3BucketUpload/releases/tag/Release)
<img width="788" height="591" alt="image" src="https://github.com/user-attachments/assets/d2e3da86-6584-45e0-8432-3997aa69a021" />
