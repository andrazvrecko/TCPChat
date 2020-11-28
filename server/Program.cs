using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace Server
{
    class Program
    {
        #region Const;
        
        const int VelSporocila = 1024;

        static Thread t = null;

        static IPAddress ipnaslov = IPAddress.Parse("127.0.0.1");  //local host
        static  IPEndPoint endPoint = new IPEndPoint(ipnaslov, 1234); // ipnaslov
        

        static string IV = "testavto"; //8 charov - inicializacijski vektor (preprecuje ponavljanje pri enkripciji)
        static string key = "nogaroka"; //8 charov
        

        static readonly object TcpClientLock = new object(); //ustvarimo lock za seznam Clientov, da ne pride do napake

        static bool alive = true;
        static readonly List<TcpClient> clientList = new List<TcpClient>(); //seznam uporabnikov
        static readonly List<string> usernames = new List<string>(); //seznam uporabniskih imen
        static readonly List<string> besede = new List<string>(); //seznam besed za igro
        static readonly List<string> ugibaneBesede = new List<string>(); //seznam besed s skritimi crkami za igro
        static readonly List<int> tocke = new List<int>(); //seznam tock
        static int besedaId = 0;//id besede
        static bool gameAlive = false; //status igre

        #endregion;

        static string EncryptData(string msg)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(msg.ToCharArray(), 0, msg.Length); //ustvarimo array bytov in ga napolnimo z sporocilom, ki ga zelimo kodirat
            DESCryptoServiceProvider encoder = new DESCryptoServiceProvider(); //class za enkripcijo in dekripcijo po AES(advanced Encryption Standard) algoritmu
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
            return Encoding.UTF8.GetString(dec, 0, dec.Length);
        }



        static string Recieve(TcpClient client) //funkcija za prejemanje
        {
            try
            {
                NetworkStream ns = client.GetStream(); //pridobi stream od uporabnika

                byte[] recv = new byte[VelSporocila]; //ustvari byte array za sporocilo

                int len = ns.Read(recv, 0, recv.Length); //dobi dolzino sporocila

                string msg = Encoding.UTF8.GetString(recv, 0, len); //sporocilo pretvori v string

                //ns.Close();

                return DecryptData(msg); //sporocilo dekodira in vrne

            }
            catch (Exception ex)
            {
                Console.WriteLine("NAPAKA:\n" + ex.Message + "\n" + ex.StackTrace);
                return null;
            }
        }



        static void Send(string msg, NetworkStream ns)
        {
            try
            {
                string encrypted = EncryptData(msg); //kodira sporocilo

                byte[] send = Encoding.UTF8.GetBytes(encrypted.ToCharArray(), 0, encrypted.Length);  //Kodirano sporocilo pretvori v array bytov.

                ns.Write(send, 0, send.Length); //poslje sporocilo
                //ns.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("NAPAKA:\n" + ex.Message + "\n" + ex.StackTrace);
            }
        }



        static void Broadcast(string msg, TcpClient handler, int a) // funkcija za posiljanje sporocila vsem uporabnikom, razen uporabniku, ki je to poslal
        {
            try
            {

                int size;
                lock(TcpClientLock) size = clientList.Count; //pridobi stevilo aktivnih uporabnikov.
  
               
                
                for (int i = 0; i < size; i++) //gre skozi aktivne uporabnike in vsem poslje sporocilo, razen v primeru ko se indeks ujema z idjem tistega, ki je sporocilo poslal
                {
                    NetworkStream stream;
                    if (i != a)
                    {
                        
                        lock(TcpClientLock) stream = clientList[i].GetStream();
                        Send(msg, stream);
                        //stream.Close();

                    }
                }
                Console.WriteLine("Broadcast poslan! \n");
                
                  
            }
            catch (Exception ex)
            {
                Console.WriteLine("NAPAKA:\n" + ex.Message + "\n" + ex.StackTrace);
            }
        }

        static void iniIgre() //inicializacija igre. Ustvari 5 besed in 5 besed s skritimi crkami, Naredi tockovnik za vse prijavljene uporabnike(imajo username) in izbere random besedo
        {
            besede.Add("wifi");
            ugibaneBesede.Add("w_f_");
            besede.Add("procesor");
            ugibaneBesede.Add("pr__e_or");
            besede.Add("RAM");
            ugibaneBesede.Add("R__");
            besede.Add("miska");
            ugibaneBesede.Add("m_s_a");
            besede.Add("besede");
            ugibaneBesede.Add("b__ed_");
            for(int i = 0; i < usernames.Count(); i++)
            {
                tocke.Add(0);
            }
            Random rnd = new Random();
            besedaId = rnd.Next(5);
            gameAlive = true;

        }

        static void Rezultati(TcpClient client,int id) //funkcija za izpis rezultatov
        {
            Console.WriteLine("Tocke: \n");
            string sporocilo = "Tocke: \n";
            for (int i = 0; i < usernames.Count(); i++)
            {
                Console.WriteLine(usernames[i] + ": " + tocke[i] + "\n");
                sporocilo = sporocilo + usernames[i] + ": "+tocke[i]+"\n";
            }
            Send(sporocilo, client.GetStream());
            Broadcast(sporocilo, client, id);
        }
        static void guess(int id, string guess, TcpClient client)//funkcija za ugibanje beseded
        {
            if (besede[besedaId] == guess) //ce se beseda ujema se povecajo tocke, beseda se izpise, izbere pa se nova beseda
            {
                tocke[id]++;

                string sporocilo = "Pravilna beseda: " + guess + "! Besedo je ugotovil " + usernames[id];
                Send(sporocilo, client.GetStream());
                Broadcast(sporocilo, client, id);
                Rezultati(client, id);

                Random rnd = new Random();
                besedaId = rnd.Next(0, 5);

                sporocilo = "Nova beseda: " + ugibaneBesede[besedaId];
                Send(sporocilo, client.GetStream());
                Broadcast(sporocilo, client, id);
            }
            else //ce se besedi ne ujemata se izpise da je narobe ugibal
            {
                string sporocilo = usernames[id] + " je ugibal narobe - " + guess;

                Send(sporocilo, client.GetStream());
                Broadcast(sporocilo, client, id);
            }
        }

        static void gamestop(TcpClient client, int id) //vsem izpise da se je igra prenehala
        {
            string sporocilo = "Igra je bila prekinjena!";
            Send(sporocilo, client.GetStream());
            Broadcast(sporocilo, client, id);
        }

        static void MojProtokol(string message, bool chatalive, TcpClient handler, int id) //funkcija za protokol
        {
            if ((message.Length > 2 && '|' == message[2]) || (message.Length > 9 && '|' == message[9]) || (message.Length>10 && '|'==message[10])) //Pravilen zapis le ce je | na 3., 10. in 11. mestu. Drugace uporabniku vrne sporocilo, da je uporabil napacno komando
            {
                string[] parts = message.Split('|'); //razdeli sporocilo na vsaj 2 dela. Pred in po |
                string head = parts[0]; //Del pred | je glava
                string body = parts[1];//Del po | je telo
                string sporocilo;
                switch (head) //glede na glavo se izvede funkcija
                {
                    case "#J": //Uporabnik se pridruzi. Izbere si svoje uporabnisko ime.
                        sporocilo = "Pridruzil se je uporabnik " + body;
                        Broadcast(sporocilo, handler, id);
                        usernames.Add(body);
                        break;
                    case "#M": //Uporabnik poslje sporocilo vsem drugim uporabnikom
                        if (usernames.ElementAtOrDefault(id) != null) //preveri ce ima uporabnisko ime
                        {
                            sporocilo = usernames[id] + ": " + body;
                            Broadcast(sporocilo, handler, id); //poslje sporocilo vsem drugim
                        }
                        else
                        {
                            sporocilo = "Najprej se pridruzite chatu z #J|vase uporabnisko ime!";
                            Send(sporocilo, handler.GetStream());
                        }
                        break;
                    case "#GAMESTOP": //prekine igro
                        if (usernames.ElementAtOrDefault(id) != null)
                        {
                            if (true == gameAlive)
                            {
                                gameAlive = false;
                                gamestop(handler, id);
                            }

                            else
                            {
                                sporocilo = "Ni aktivne igre!";
                                Send(sporocilo, handler.GetStream());
                            }
                        }
                        else
                        {
                            sporocilo = "Najprej se pridruzite chatu z #J|vase uporabnisko ime!";
                            Send(sporocilo, handler.GetStream());
                        }
                        break;
                    case "#GAMESTART": //zacne igro
                        if (usernames.ElementAtOrDefault(id) != null)
                        {
                            if (false == gameAlive)
                            {
                                iniIgre();
                                sporocilo = "Beseda: " + ugibaneBesede[besedaId];
                                Send(sporocilo, handler.GetStream());
                                Broadcast(sporocilo, handler, id);
                            }
                            else
                            {
                                sporocilo = "Obstaja aktivna igra!";
                                Send(sporocilo, handler.GetStream());
                            }
                        }
                        else
                        {
                            sporocilo = "Najprej se pridruzite chatu z #J|vase uporabnisko ime!";
                            Send(sporocilo, handler.GetStream());
                        }
                 
                        break;
                    case "#U": //ugiba
                        if (usernames.ElementAtOrDefault(id) != null)
                        {
                            if (true == gameAlive)
                            {
                                guess(id, body, handler);
                            }
                            else
                            {
                                sporocilo = "Ni aktivne igre!";
                                Send(sporocilo, handler.GetStream());
                            }
                        }
                        else
                        {
                            sporocilo = "Najprej se pridruzite chatu z #J|vase uporabnisko ime!";
                            Send(sporocilo, handler.GetStream());
                        }
                        break;
                    case "#X": //prekine chat
                        chatalive = false;
                        break;
                    default:
                        break;

                }
            }
            else
            {
                string ErrorMessage = "Napaka pri razbiranju sporocila. Upostevajte protokol! \n";
                Send(ErrorMessage, handler.GetStream());
            }
                       
        }

        public static void ClientChat(object count) //funkcija za chat
        {
            int id = (int)count; //id uporabnika -> izvira iz count v Main
            TcpClient handler;
            lock(TcpClientLock) handler = clientList[id]; //Ustvar TcpClient in doda client iz seznama vseh clientov
            bool chatalive = true;
            
            while (chatalive) //loop do prekinitve
            {

                try
                {
                    string message = Recieve(handler); //caka dokler ne prejme sporocila

                    Console.WriteLine("Msg received from user" + id + "[" + handler.Client.RemoteEndPoint.ToString() + "]: " + message); //izpis prejetega sporocila
                    MojProtokol(message, chatalive, handler, id); //protokol
                   
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Uporabnik je prekinil povezavo!\n");
                    //lock (TcpClientLock) clientList.Remove(handler);
                    //handler.Client.Shutdown(SocketShutdown.Both);
                    //handler.Close();
                    //handler.GetStream().Close();
                    t.Join();
                }
                
            }
        }
        

        static void Main(string[] args)
        {
            
            int count = 0; //stevilo prikljucenih uporabnikov, tudi id uporabnika

            TcpListener listener = new TcpListener(endPoint); //ustvarjanje TcpListenerja
            listener.Start(); //zacetek poslusanja za "nove uporabnike"

            while (alive) //konstanten loop
            {
                TcpClient client = listener.AcceptTcpClient(); //ko najde uporabnika ga sprejme
                
                
                lock (TcpClientLock) clientList.Add(client); //upoarbnika doda na seznam uporabnikov
                Console.WriteLine("Uporabnik"+count+" se je prikljucil."); 

                t = new Thread(ClientChat); //za novega uporabnika ustvari novo nit
                t.Start(count); //zacetek nove niti
                count++; 
            }
            listener.Stop();
            Console.WriteLine("Quit.."); //pritisnite tipko da se program zakljuci
            Console.ReadKey();
        }
    }
}
