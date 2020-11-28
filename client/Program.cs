using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using System.IO;

namespace Client
{
    class Program
    {
        #region Const;
        const int VelSporocila = 1024;

        static IPAddress ipnaslov = IPAddress.Parse("127.0.0.1");  //local host
        static IPEndPoint RemoteEP = new IPEndPoint(ipnaslov, 1234); // ipnaslov
        

        static string IV = "testavto"; //8 charov - inicializacijski vektor (preprecuje ponavljanje pri enkripciji)
        static string key = "nogaroka"; //8 charov
        #endregion;   

        static string EncryptData(string msg)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(msg.ToCharArray(), 0, msg.Length); //ustvarimo array bytov in ga napolnimo z sporocilom, ki ga zelimo kodirat
            DESCryptoServiceProvider encoder = new DESCryptoServiceProvider(); //class za enkripcijo in dekripcijo po DES(data Encryption Standard) algoritmu
            encoder.BlockSize = 64; //nastavimo dolzina bit stringa IV
            encoder.KeySize = 64; //nastavimo dolzina bit stringa kljuca
            encoder.Key = Encoding.UTF8.GetBytes(key.ToCharArray(), 0, key.Length); //encoderju nastavimo kljuc v bytih
            encoder.IV = Encoding.UTF8.GetBytes(IV.ToCharArray(), 0, IV.Length); //encoderju nastavimo IV v bytih
            encoder.Padding = PaddingMode.PKCS7; //Nastavimo padding na PKCS7 (standard). S tem preprecimo predvidljivost.
            encoder.Mode = CipherMode.CBC; //Nastavimo nacin na CBC(cipher block chaining)

            ICryptoTransform crypt = encoder.CreateEncryptor(encoder.Key, encoder.IV); //ustvari enkriptor

            byte[] enc = crypt.TransformFinalBlock(bytes, 0, bytes.Length);//kriptira sporocilo
            crypt.Dispose(); //odstrani neuporabne resource
            return Convert.ToBase64String(enc); //pretvori byte v string
            
            
        }

        static string DecryptData(string msg)
        {
            
            byte[] bytes = Convert.FromBase64String(msg);
            DESCryptoServiceProvider decoder = new DESCryptoServiceProvider();
            decoder.BlockSize = 64;
            decoder.KeySize = 64;
            decoder.Key = Encoding.UTF8.GetBytes(key.ToCharArray(), 0, key.Length);
            decoder.IV = Encoding.UTF8.GetBytes(IV.ToCharArray(), 0, IV.Length);
            decoder.Padding = PaddingMode.PKCS7;
            decoder.Mode = CipherMode.CBC;

            ICryptoTransform crypt = decoder.CreateDecryptor(decoder.Key, decoder.IV);
            byte[] dec = crypt.TransformFinalBlock(bytes, 0, bytes.Length);
            crypt.Dispose();
            //return dec;
            return Encoding.UTF8.GetString(dec, 0, dec.Length);
        }

   


        static string Recieve(TcpClient client)
        {
            try
            {
                NetworkStream ns = client.GetStream(); 
                byte[] recv = new byte[VelSporocila];
                int len = ns.Read(recv, 0, recv.Length);
                string msg = Encoding.UTF8.GetString(recv, 0, len);
                //ns.Close();
                return DecryptData(msg);
               
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error\n" + ex.Message + "\n" + ex.StackTrace);
                return null;
            }
        }

        static void Send(string msg, TcpClient client)
        {
            try
            {
                NetworkStream ns = client.GetStream();
                string encrypted = EncryptData(msg);
                byte[] send = Encoding.UTF8.GetBytes(encrypted.ToCharArray(), 0, encrypted.Length);
                //Console.WriteLine(Convert.ToBase64String(send));
                
                ns.Write(send, 0, send.Length);
                //ns.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error\n" + ex.Message + "\n" + ex.StackTrace);
            }
        }


        public static void getData(object cl) //Nit v kateri prejemamo sporocila
        {
            TcpClient client = (TcpClient)cl; //odjemalec
            NetworkStream ns;
            while (true) //loop ki caka, da dobi sporocilo od streznika
            {
                ns = client.GetStream(); //dobimo stream od odjemmalca
                string response = Recieve(client);

                Console.WriteLine(response);
                //ns.Close();
            }
           
        }

        static void Main(string[] args)
        {
            TcpClient sender = new TcpClient(); //inicializacija TcpClienta
            sender.Connect(RemoteEP); //Client se poveze na streznik
            Console.WriteLine("socket connected to: " + sender.Client.RemoteEndPoint.ToString());

            Thread t = new Thread(getData); //Nova nit
            t.Start(sender); //Zagon niti
            while (true) //neskoncna zanka, ki caka na sporocilo
            {
              
                string ukaz = Console.ReadLine();

                if (ukaz == "q")
                {
                    sender.Close();
                    break;

                }
                try
                {

                  
                    Send(ukaz, sender);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Napaka " + ex.Message + " " + ex.StackTrace);
                }
            }
            Console.WriteLine("Quit..");
            Console.ReadKey();
        }


    }
}
