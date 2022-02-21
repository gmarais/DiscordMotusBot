using System;
using System.Security.Cryptography; 
using System.Text;
using System.IO;

namespace DiscordMotusBot
{
    public class EncryptedSaves
    {
        private static string _cryptKey;

        static EncryptedSaves()
        {
            _cryptKey = System.Environment.GetEnvironmentVariable("CRYPT_KEY");
        }

        public static bool SaveExists(string entry)
        {
            return File.Exists("Saves/" + entry + ".save");
        }
      
        public static void Save(string entry, string contents)
        {
            if (SaveExists(entry))
                File.Delete("Saves/" + entry + ".save");
            File.WriteAllText("Saves/" + entry + ".save", Encrypt(contents));
        }
      
        public static string Load(string entry)
        {
            if (SaveExists(entry))
            {
                string contents = File.ReadAllText("Saves/" + entry + ".save");
                return Decrypt(contents);
            }
            return "";
        }

        private static string Encrypt(string input)
        {  
            byte[] inputArray = UTF8Encoding.UTF8.GetBytes(input);  
            TripleDESCryptoServiceProvider tripleDES = new TripleDESCryptoServiceProvider();  
            tripleDES.Key = UTF8Encoding.UTF8.GetBytes(_cryptKey);  
            tripleDES.Mode = CipherMode.ECB;  
            tripleDES.Padding = PaddingMode.PKCS7;  
            ICryptoTransform cTransform = tripleDES.CreateEncryptor();  
            byte[] resultArray = cTransform.TransformFinalBlock(inputArray, 0, inputArray.Length);  
            tripleDES.Clear();  
            return Convert.ToBase64String(resultArray, 0, resultArray.Length);  
        }

        private static string Decrypt(string input)  
        {  
            byte[] inputArray = Convert.FromBase64String(input);  
            TripleDESCryptoServiceProvider tripleDES = new TripleDESCryptoServiceProvider();  
            tripleDES.Key = UTF8Encoding.UTF8.GetBytes(_cryptKey);  
            tripleDES.Mode = CipherMode.ECB;  
            tripleDES.Padding = PaddingMode.PKCS7;  
            ICryptoTransform cTransform = tripleDES.CreateDecryptor();  
            byte[] resultArray = cTransform.TransformFinalBlock(inputArray, 0, inputArray.Length);  
            tripleDES.Clear();   
            return UTF8Encoding.UTF8.GetString(resultArray);  
        }
    }
}