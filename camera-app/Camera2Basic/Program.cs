using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace httpPost
{
	class Program
    {
        public static readonly HttpClient client = new HttpClient();


        public static async Task<string> getCarId(string imageEncode, string fileName, byte[] fileData, string regions, string boundary)
        {
            var values = new Dictionary<string, string>
            {
                { "upload", imageEncode }

            };

            var content = new FormUrlEncodedContent(values);
            var _CredentialBase64 = "02e9be66049781b98e82a4882fa5fcd7ff562ec0";
            string contentType = "multipart/form-data; boundary=" + boundary;

            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                fileData = File.ReadAllBytes(fileName);
            }
            else
                fileName = DateTime.Now.Ticks.ToString();

            if (fileData != null && fileData != null && fileData.Length > 0)
            {

                byte[] formData = GetMultipartFormData(fileName, fileData, regions, boundary);
                HttpWebRequest request = WebRequest.Create("https://api.platerecognizer.com/v1/plate-reader/") as HttpWebRequest;
                // Set up the request properties.
                request.Method = "POST";
                request.ContentType = contentType;
                request.UserAgent = ".NET Framework CSharp Client";
                request.CookieContainer = new CookieContainer();
                request.ContentLength = formData.Length;
                request.Headers.Add("Authorization", "Token " + _CredentialBase64);

                // Send the form data to the request.
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(formData, 0, formData.Length);
                    requestStream.Close();
                }
                Console.WriteLine(request.Headers.ToString());
                // Console.WriteLine(request.GetRequestStream.ToString());

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                WebHeaderCollection header = response.Headers;
                var encoding = ASCIIEncoding.ASCII;
                using (var reader = new System.IO.StreamReader(response.GetResponseStream(), encoding))
                {
                    string responseText = reader.ReadToEnd();
                    string plate;
                    try
                    {
                        //result = Utils.JsonSerializer<PlateReaderResult>.DeSerialize(responseText);
                        plate = responseText.Split(new[] { "\"plate\":\"" }, StringSplitOptions.None)[1].Split(new[] { '"' }, StringSplitOptions.None)[0];
                        //PlatNumber deserializedProduct = JsonConvert.DeserializeObject<PlatNumber>(responseText);
                    }
                    catch
                    {
                        plate = "no number";
                    }
                    return plate;
                }
            }
            return "failed";
        }
        private static byte[] GetMultipartFormData(string fileName, byte[] fileData, string regions, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                // Add just the first part of this param, since we will write the file data directly to the Stream
                string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n",
                    boundary,
                    "upload",
                    fileName,
                    "application/octet-stream");

                formDataStream.Write(Encoding.UTF8.GetBytes(header), 0, Encoding.UTF8.GetByteCount(header));

                // Write the file data directly to the Stream, rather than serializing it to a string.
                formDataStream.Write(fileData, 0, fileData.Length);
            }

            if (!string.IsNullOrWhiteSpace(regions))
            {
                foreach (var region in regions.Split(','))
                    if (!string.IsNullOrWhiteSpace(region))
                    {
                        formDataStream.Write(Encoding.UTF8.GetBytes("\r\n"), 0, Encoding.UTF8.GetByteCount("\r\n"));
                        string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                            boundary,
                            "regions",
                            region);
                        formDataStream.Write(Encoding.UTF8.GetBytes(postData), 0, Encoding.UTF8.GetByteCount(postData));
                    }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(Encoding.UTF8.GetBytes(footer), 0, Encoding.UTF8.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }
        static async Task Main()
        {

            string path = "C:\\Users\\home\\Desktop\\korde.gif";
            string base64String = "";
            byte[] bytes = Encoding.ASCII.GetBytes(Convert.ToBase64String(File.ReadAllBytes(path)));
            base64String = bytes.ToString();
            string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
            string p = await getCarId(base64String, path, bytes, "il", formDataBoundary);
            Console.WriteLine("the plate number is : " + p.ToString());
        }
    }
}
