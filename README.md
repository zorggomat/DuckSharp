# DuckSharp
This is multifunctional .NET keylogger.

Features:
- Reads settings from a resource file
- Sends the log by email
- If mail is unavailable, it writes in the file
- If writing to the file is also unavailable, stores the entire log with the ability to write to a flash drive
- Copies itself to disk and adds copy to autorun 
- Checks the title of the active window
- Deletes itself if debugging is detected
- encrypts logs with AES256

To decrypt the logs you can use [DecryptorSharp](https://github.com/zorggish/DecryptorSharp).
