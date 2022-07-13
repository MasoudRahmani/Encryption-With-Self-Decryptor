using System.IO;

namespace Encryptor.Helper
{
    public static class StreamHelper
    {

        /// <summary>
        /// https://stackoverflow.com/questions/221925/creating-a-byte-array-from-a-stream
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] ReadFully(this Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}


