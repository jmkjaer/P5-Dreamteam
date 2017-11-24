﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using WarehouseAI.Representation;
using WarehouseAI.UI;

namespace WarehouseAI
{
    public static class Program
    {
        private const string CommandList = "\\help";
        private static readonly Dictionary<string, Delegate> Commands = new Dictionary<string, Delegate>();

        private static void Main(string[] args)
        {
            IController consoleController = new ConsoleController();
            WarehouseRepresentation warehouse = new WarehouseRepresentation();
            ItemDatabase itemDatabase = new ItemDatabase();

            consoleController.warehouse = warehouse;
            consoleController.itemDatabase = itemDatabase;
            warehouse.ItemDatabase = itemDatabase;

            warehouse.Inintialize();

            consoleController.Start(args);
            
        }
       
//        /// <summary>
//        /// Prints the list of clients
//        /// </summary>
//        private static void ListAllClients()
//        {
//            if (_server.ClientSockets.Count < 1)
//            {
//                Console.WriteLine("There is currently no clients connected to the server");
//                return;
//            }
//            foreach (Socket serverClientSocket in _server.ClientSockets)
//            {
//                Console.WriteLine(serverClientSocket.RemoteEndPoint.ToString());
//            }
//        }
    }
}
