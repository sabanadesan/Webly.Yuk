using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;

namespace Webly.Yuk
{
    public class MyWebServer
    {
        private TcpListener myListener;
        private String myIPAddress = "127.0.0.1";
        String sMyWebServerRoot = "MyPersonalwebServer";

        private int port = 5050; // Select any free port you wish   
                                 //The constructor which make the TcpListener start listening on th  
                                 //given port. It also calls a Thread on the method StartListen().   
        public MyWebServer()
        {
            try
            {
                String server = Dns.GetHostName();
                IPAddress iIPAddress = IPAddress.Parse(myIPAddress);

                //start listing on the given port  
                myListener = new TcpListener(iIPAddress, port);
                myListener.Start();
                Console.WriteLine("Web Server Running... Press ^C to Stop...");
                //start the thread which calls the method 'StartListen'  
                Thread th = new Thread(new ThreadStart(StartListen));
                th.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred while Listening :" + e.ToString());
            }
        }

        public void StartListen()  
        {  


            while (true)  
            {  
                //Accept a new connection  
                Socket mySocket = myListener.AcceptSocket();  
                Console.WriteLine("Socket Type " + mySocket.SocketType);
                
                if (mySocket.Connected)  
                {
                    Boolean isContinue = true;

                    Console.WriteLine("\nClient Connected!!\n==================\n CLient IP {0}\n", mySocket.RemoteEndPoint);  
                    //make a byte array and receive data from the client   
                    Byte[] bReceive = new Byte[1024];  
                    int i = mySocket.Receive(bReceive, bReceive.Length, 0);  
                    //Convert Byte to String  
                    string sBuffer = Encoding.ASCII.GetString(bReceive);  
                    //At present we will only deal with GET type  
                    if (sBuffer.Substring(0, 3) != "GET")  
                    {  
                        Console.WriteLine("Only Get Method is supported..");
                        isContinue = false;
                    }

                    if (isContinue)
                        doRequest(sBuffer, mySocket);

                    mySocket.Close();
                }  
            }  
        }

        private Boolean doRequest(string sBuffer, Socket mySocket)
        {
            int iStartPos = 0;
            String sRequest;
            String sDirName;
            String sRequestedFile;
            String sErrorMessage;
            String sLocalDir;

            String sPhysicalFilePath = "";
            String sFormattedMessage = "";
            String sResponse = "";

            // Look for HTTP request  
            iStartPos = sBuffer.IndexOf("HTTP", 1);
            // Get the HTTP text and version e.g. it will return "HTTP/1.1"  
            string sHttpVersion = sBuffer.Substring(iStartPos, 8);
            // Extract the Requested Type and Requested file/directory  
            sRequest = sBuffer.Substring(0, iStartPos - 1);
            //Replace backslash with Forward Slash, if Any  
            sRequest.Replace("\\", "/");
            //If file name is not supplied add forward slash to indicate   
            //that it is a directory and then we will look for the   
            //default file name..  
            if ((sRequest.IndexOf(".") < 1) && (!sRequest.EndsWith("/")))
            {
                sRequest = sRequest + "/";
            }
            //Extract the requested file name  
            iStartPos = sRequest.LastIndexOf("/") + 1;
            sRequestedFile = sRequest.Substring(iStartPos);
            //Extract The directory Name  
            sDirName = sRequest.Substring(sRequest.IndexOf("/"), sRequest.LastIndexOf("/") - 3);

            /////////////////////////////////////////////////////////////////////  
            // Identify the Physical Directory  
            /////////////////////////////////////////////////////////////////////  

            //Get the Virtual Directory
            if (sDirName == "/")
            {
                sDirName = "";
            }

            sLocalDir = GetLocalPath(sDirName);

            Console.WriteLine("Directory Requested : " + sLocalDir);
            //If the physical directory does not exists then  
            // dispaly the error message  
            if (sLocalDir.Length == 0)
            {
                sErrorMessage = "<H2>Error!! Requested Directory does not exists</H2><Br>";
                //sErrorMessage = sErrorMessage + "Please check data\\Vdirs.Dat";  
                //Format The Message  
                SendHeader(sHttpVersion, "", sErrorMessage.Length, " 404 Not Found", ref mySocket);
                //Send to the browser  
                SendToBrowser(sErrorMessage, ref mySocket);
                mySocket.Close();
                return false;
            }

            /////////////////////////////////////////////////////////////////////  
            // Identify the File Name  
            /////////////////////////////////////////////////////////////////////  
            //If The file name is not supplied then look in the default file list  
            if (sRequestedFile.Length == 0)
            {
                // Get the default filename  
                sRequestedFile = GetTheDefaultFileName(sLocalDir);
                if (sRequestedFile == "")
                {
                    sErrorMessage = "<H2>Error!! No Default File Name Specified</H2>";
                    SendHeader(sHttpVersion, "", sErrorMessage.Length, " 404 Not Found",
                    ref mySocket);
                    SendToBrowser(sErrorMessage, ref mySocket);
                    mySocket.Close();
                    return false;
                }
            }

            /////////////////////////////////////////////////////////////////////  
            // Get TheMime Type  
            /////////////////////////////////////////////////////////////////////  
            String sMimeType = GetMimeType(sRequestedFile);
            //Build the physical path  
            sPhysicalFilePath = Path.Combine(sLocalDir, sRequestedFile);
            Console.WriteLine("File Requested : " + sPhysicalFilePath);

            if (File.Exists(sPhysicalFilePath) == false)
            {
                sErrorMessage = "<H2>404 Error! File Does Not Exists...</H2>";
                SendHeader(sHttpVersion, "", sErrorMessage.Length, " 404 Not Found", ref mySocket);
                SendToBrowser(sErrorMessage, ref mySocket);
                Console.WriteLine(sFormattedMessage);
            }
            else
            {
                int iTotBytes = 0;
                sResponse = "";
                FileStream fs = new FileStream(sPhysicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                // Create a reader that can read bytes from the FileStream.  
                BinaryReader reader = new BinaryReader(fs);
                byte[] bytes = new byte[fs.Length];
                int read;
                while ((read = reader.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Read from the file and write the data to the network  
                    sResponse = sResponse + Encoding.ASCII.GetString(bytes, 0, read);
                    iTotBytes = iTotBytes + read;
                }
                reader.Close();
                fs.Close();
                SendHeader(sHttpVersion, sMimeType, iTotBytes, " 200 OK", ref mySocket);
                SendToBrowser(bytes, ref mySocket);
                //mySocket.Send(bytes, bytes.Length,0);  
            }

            return true;
        }

        public String GetAbsolutePath()
        {
            return GetAbsolutePath("");
        }

        public String GetAbsolutePath(String path)
        {
            String root = Path.GetPathRoot(Directory.GetCurrentDirectory());
            String[] paths = new string[] { root, sMyWebServerRoot, path };
            String fullPath = Path.Combine(paths);

            return fullPath;
        }

        public string GetMimeType(string sRequestedFile)
        {
            StreamReader sr;
            String sLine = "";
            String sMimeType = "";
            String sFileExt = "";
            String sMimeExt = "";
            // Convert to lowercase  
            sRequestedFile = sRequestedFile.ToLower();
            int iStartPos = sRequestedFile.IndexOf(".");
            sFileExt = sRequestedFile.Substring(iStartPos);
            try
            {
                String path = "data\\Mime.Dat";
                String full_path = GetAbsolutePath(path);

                //Open the Vdirs.dat to find out the list virtual directories  
                sr = new StreamReader(full_path);
                while ((sLine = sr.ReadLine()) != null)
                {
                    sLine.Trim();
                    if (sLine.Length > 0)
                    {
                        //find the separator  
                        iStartPos = sLine.IndexOf(";");
                        // Convert to lower case  
                        sLine = sLine.ToLower();
                        sMimeExt = sLine.Substring(0, iStartPos);
                        sMimeType = sLine.Substring(iStartPos + 1);
                        if (sMimeExt == sFileExt)
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred : " + e.ToString());
            }
            if (sMimeExt == sFileExt)
                return sMimeType;
            else
                return "";
        }

        public string GetLocalPath(string sDirName)
        {
            StreamReader sr;
            String sLine = "";
            String sVirtualDir = "";
            String sRealDir = "";
            int iStartPos = 0;
            String sPath = "";
            //Remove extra spaces  
            sDirName.Trim();
            // Convert to lowercase  
            sDirName = sDirName.ToLower();
            try
            {
                String path = "data\\VDirs.Dat";
                String full_path = GetAbsolutePath(path);

                //Open the Vdirs.dat to find out the list virtual directories  
                sr = new StreamReader(full_path);
                while ((sLine = sr.ReadLine()) != null)
                {
                    //Remove extra Spaces  
                    sLine.Trim();
                    if (sLine.Length > 0)
                    {
                        //find the separator  
                        iStartPos = sLine.IndexOf(";");
                        // Convert to lowercase  
                        sLine = sLine.ToLower();
                        sVirtualDir = sLine.Substring(0, iStartPos);
                        sRealDir = sLine.Substring(iStartPos + 1);
                        sPath = Path.Combine(GetAbsolutePath(), sDirName);
                        if (sVirtualDir.ToLower() == sPath.ToLower() )
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred : " + e.ToString());
            }
            if (sVirtualDir == sDirName)
                return sRealDir;
            else if (sDirName == "")
                return sVirtualDir;
            else
                return "";
        }

        public string GetTheDefaultFileName(string sLocalDirectory)
        {
            StreamReader sr;
            String sLine = "";
            try
            {
                String path = "data\\Default.Dat";
                String full_path = GetAbsolutePath(path);

                //Open the default.dat to find out the list  
                // of default file  
                sr = new StreamReader(full_path);
                while ((sLine = sr.ReadLine()) != null)
                {
                    //Look for the default file in the web server root folder  
                    if (File.Exists(Path.Combine(sLocalDirectory, sLine)) == true)
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred : " + e.ToString());
            }
            if (File.Exists(Path.Combine(sLocalDirectory, sLine)) == true)
                return sLine;
            else
                return "";
        }

        public void SendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, ref Socket mySocket)
        {
            String sBuffer = "";
            // if Mime type is not provided set default to text/html  
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";// Default Mime Type is text/html  
            }
            sBuffer = sBuffer + sHttpVersion + sStatusCode + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n\r\n";
            Byte[] bSendData = Encoding.ASCII.GetBytes(sBuffer);
            SendToBrowser(bSendData, ref mySocket);
            Console.WriteLine("Total Bytes : " + iTotBytes.ToString());
        }

        public void SendToBrowser(String sData, ref Socket mySocket)
        {
            SendToBrowser(Encoding.ASCII.GetBytes(sData), ref mySocket);
        }
        public void SendToBrowser(Byte[] bSendData, ref Socket mySocket)
        {
            int numBytes = 0;
            try
            {
                if (mySocket.Connected)
                {
                    if ((numBytes = mySocket.Send(bSendData, bSendData.Length, 0)) == -1)
                        Console.WriteLine("Socket Error cannot Send Packet");
                    else
                    {
                        Console.WriteLine("No. of bytes send {0}", numBytes);
                    }
                }
                else Console.WriteLine("Connection Dropped....");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred : {0} ", e);
            }
        }
    }
}
