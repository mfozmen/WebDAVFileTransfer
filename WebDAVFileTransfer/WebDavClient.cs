using System;
using System.IO;
using System.Net;

namespace wdft
{
    public class WebDavClient
    {
        private const int _timeout = 3000;

        #region Constructors

        public WebDavClient()
        {
        }

        public WebDavClient(NetworkCredential Credential)
        {
            this.Credential = Credential;
        }

        #endregion Constructors

        #region GetSet

        public NetworkCredential Credential { get; set; }

        #endregion GetSet

        #region PrivateMethods

        private bool IsDirExists(string path)
        {
            HttpWebResponse res = null;
            try
            {
                HttpWebRequest req = HttpWebRequest.Create(path) as HttpWebRequest;
                req.Credentials = Credential;
                req.Headers.Add("Translate: f");
                req.Method = WebRequestMethods.Http.Head;

                if (req.HaveResponse) // Eğer requeste response varsa klasör var demektir.
                {
                    return true;
                }
                res = (HttpWebResponse)req.GetResponse();

                if (res.StatusCode == HttpStatusCode.OK || res.StatusCode == HttpStatusCode.NoContent)
                {
                    res.Close();
                    res.Dispose();
                    return true;
                }
                else
                {
                    res.Close();
                    res.Dispose();
                    return false;
                }
            }
            catch (WebException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool CreateDir(string path)
        {
            HttpWebResponse res = null;

            try
            {
                if (!IsDirExists(path))
                {
                    HttpWebRequest req = HttpWebRequest.Create(path) as HttpWebRequest;
                    req.Credentials = Credential;
                    req.Method = WebRequestMethods.Http.MkCol;

                    Stream s = req.GetRequestStream();

                    res = req.GetResponse() as HttpWebResponse;
                    if (res.StatusCode == HttpStatusCode.Created)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion PrivateMethods

        #region PublicMethods

        /// <summary>
        /// Uploads a single file
        /// </summary>
        /// <param name="UploadPath">Upload URL</param>
        /// <param name="FilePath">Uploading file</param>
        /// <returns></returns>
        public bool UploadFile(string UploadPath, string FilePath)
        {
            if (CreateDir(UploadPath)) // Dosyaları upload edebilmek için önce klasörü oluşturmak gerek. Klasör yoksa oluşturmuyor.
            {
                string url = UploadPath + "/" + Path.GetFileName(FilePath);

                FileInfo _fileinfo = new FileInfo(FilePath);
                long _filelength = _fileinfo.Length;

                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);

                request.Credentials = Credential;
                request.Method = WebRequestMethods.Http.Put;
                request.ContentLength = _fileinfo.Length;
                request.PreAuthenticate = true;

                //*** This is required for our WebDav server ***
                request.SendChunked = true;
                request.Headers.Add("Translate: f");
                request.AllowWriteStreamBuffering = true;

                Stream s;

                //Send the request to the server, and get the
                //server's (file) Stream in return.
                s = request.GetRequestStream();

                //C:  After the server has given us a stream, we can begin to write to it.
                //
                //    Note:  The data is not actually being sent to the server
                //    here, it is written to a stream in memory.
                //    The data is actually sent below when the Response is retrieved
                //    from the server.

                FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read);

                //Create the buffer for storing the bytes read from the file
                int byteTransferRate = 1024;
                byte[] bytes = new byte[byteTransferRate];
                int bytesRead = 0;
                long totalBytesRead = 0;

                //Read from the file and write it to the server's stream.
                do
                {
                    //Read from the file
                    bytesRead = fs.Read(bytes, 0, bytes.Length);

                    if (bytesRead > 0)
                    {
                        totalBytesRead += bytesRead;

                        //Write to stream
                        s.Write(bytes, 0, bytesRead);

                        //if (pb.Value % 500 == 0)
                        //     Application.DoEvents();
                    }
                } while (bytesRead > 0);

                //Close the server stream
                s.Close();
                s.Dispose();
                s = null;

                //Close the file
                fs.Close();
                fs.Dispose();
                fs = null;

                //    Although we have finished writing the file to the stream, the file
                //    has not been uploaded yet.  If we exited here without continuing,
                //    the file would not be uploaded.

                //    Now we have to send the data to the server

                HttpWebResponse response = null;

                //***  Send the data to the server
                //  Note:  When we get the response from the server, we
                //  are actually sending the data to the server, and receiving
                //  the server's response to it in return.

                //  If we did not perform this step, the file would not be uploaded.
                response = (HttpWebResponse)request.GetResponse();

                HttpStatusCode code = response.StatusCode;

                response.Close();
                response.Dispose();

                // If file uploaded completely
                if (totalBytesRead == _filelength && code == HttpStatusCode.Created)
                {
                    return true;
                }
                else
                {
                    throw new Exception("File couldn't be uploaded!\r\n File Path: " + FilePath);
                }
            }
            return false;
        }

        public bool DownloadFile(string URL, string SaveToPath)
        {
            if (!File.Exists(SaveToPath))
            {
                int bytesRead = 0;
                long totalBytesRead = 0;
                long contentLength = 0;

                HttpWebRequest Request = (HttpWebRequest)HttpWebRequest.Create(URL);
                Request.Method = WebRequestMethods.Http.Get;
                Request.Credentials = Credential;

                Request.Headers.Add("Translate", "f");
                Request.PreAuthenticate = true;

                HttpWebResponse Response = (HttpWebResponse)Request.GetResponse();

                contentLength = Convert.ToInt64(Response.GetResponseHeader("Content-Length"));

                using (Stream s = Response.GetResponseStream())
                {
                    FileInfo finfo = new FileInfo(SaveToPath);

                    if (!Directory.Exists(finfo.Directory.FullName))
                    {
                        Directory.CreateDirectory(finfo.Directory.FullName);
                    }
                    using (FileStream fs = File.OpenWrite(SaveToPath))
                    {
                        byte[] content = new byte[4096];

                        do
                        {
                            bytesRead = s.Read(content, 0, content.Length);
                            if (bytesRead > 0)
                            {
                                totalBytesRead += bytesRead;
                                fs.Write(content, 0, bytesRead);
                            }
                        } while (bytesRead > 0);
                    }
                }
                Response.Close();
                Response.Dispose();

                if (totalBytesRead != contentLength)
                {
                    throw new Exception("File couldn't be downloaded!\r\n URL: " + URL);
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        #endregion PublicMethods
    }
}