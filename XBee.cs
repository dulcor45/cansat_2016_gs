﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;
namespace CanSatGroundStation
{
    // Delegate methods to be notified for events

    public delegate void XBeeIncomingPacketAvailable(XBeeIncomingPacket packet);
    public delegate void TelemetryDataAvailableHandler(TelemetryPacket packet);
    public delegate void XBeeRawByteAvailableHandler(byte rawByte);


   

    public class XBeeIncomingPacket
    {
        private const byte RECIEVE_PACKET_FRAME_TYPE = 0x90;
        private const byte TRANSMIT_STATUS_FRAME_TYPE = 0x8B;

        public enum XBEE_FRAME_TYPE
        {
            RECIEVE_PACKET = RECIEVE_PACKET_FRAME_TYPE,
            TRANSMIT_STATUS = TRANSMIT_STATUS_FRAME_TYPE,
            UNKNOWN_FRAME_TYPE
        }

        private byte[] packetLengthBytes = new byte[2];
        private XBEE_FRAME_TYPE frameType;
        private byte[] packetData;

        public XBEE_FRAME_TYPE FrameType
        {
            get
            {
                return frameType;
            }
            set
            {
                frameType = value;
            }
        }

        public byte[] PacketData
        {
            get
            {
                return packetData;
            }
        }

        public int PacketDataLength
        {
            get
            {
                return (packetLengthBytes[1] | packetLengthBytes[0] << 8) - 1; //Excludes the frame type byte
            }
        }

        public void setPacketLengthByte(byte value, int bytePos)
        {
            if(bytePos < packetLengthBytes.Length)
            {
                packetLengthBytes[bytePos] = value;
                if(bytePos == 1)
                {
                    // We have all length bytes. We can initialize the packetData array
                    packetData = new byte[PacketDataLength];
                }
            }
        }

        public void setPacketDataByte(byte byteValue, int bytePos)
        {
            if (packetData != null && bytePos < packetData.Length)
            {
                packetData[bytePos] = byteValue;
            }
        }

        public void verifyChecksum(byte checksumByte)
        {
            //TODO:
        }
       
        public static byte[] createIncomingRecievePacket(byte[] packetData)
        {
            byte[] recievePacket = new byte[16 + packetData.Length];

            //Start delimiter
            recievePacket[0] =  XBee.XBEE_PACKET_START_DELIMITER;

            //Length bytes between, length field and checksum
            int length = 12 + packetData.Length;
            recievePacket[1] = (byte)(length >> 8); //MSB
            recievePacket[2] = (byte)(length & 0x00FF); //LSB

            //Frame type
            recievePacket[3] = RECIEVE_PACKET_FRAME_TYPE;

            //Next 8 bytes are 64 bit source address, leave empty

            //Next 2 bytes are 16 bit source address, leave empty

            //Next 1 byte is recieve option
            recievePacket[14] = 0x41;

            for(int i=0;  i < packetData.Length; i++)
            {
                recievePacket[i + 15] = packetData[i];
            }

            //TODO: Leave checksum byte empty 

            return recievePacket;
        }

        public static XBEE_FRAME_TYPE toFrameType(byte frameTypeByte)
        {
            switch(frameTypeByte)
            {
                case RECIEVE_PACKET_FRAME_TYPE:
                    return XBEE_FRAME_TYPE.RECIEVE_PACKET;
                case TRANSMIT_STATUS_FRAME_TYPE:
                    return XBEE_FRAME_TYPE.TRANSMIT_STATUS;
                default:
                    return XBEE_FRAME_TYPE.UNKNOWN_FRAME_TYPE;
            }
        }

    }


    class XBee
    {
        private static volatile XBee xbee;
        private static object syncRoot = new Object();        
        private static SerialPort serialPort;

        public const byte XBEE_PACKET_START_DELIMITER = 0x7E;
        private static int INCOMING_PACKET_BUFFER_SIZE = 200;

        public static event XBeeIncomingPacketAvailable xbeeIncomingPacketAvaialbleHandler;
        public static event XBeeRawByteAvailableHandler xbeeRawByteAvaialbleHandler;

        private static byte[] incomingPacketBuffer = new byte[INCOMING_PACKET_BUFFER_SIZE];

        private static bool xbeeAPIFrameParsingStarted = false;
        private static int incomingPacketFrameIndex = 0;
        private static XBeeIncomingPacket incomingPacket;
 
        private XBee(){}

        public static XBee Instance
        {
            get
            {
                // use a lock for additional thread safety
                lock (syncRoot)
                {
                    if (xbee == null)
                    {
                        xbee = new XBee();                        
                        serialPort = new SerialPort();
                        serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPortDataReceivedHandler);
                    }
                }
                return xbee;
            }
        }        

        public static void IncomingByteHandler(byte incomingByte)
        {
            xbeeRawByteAvaialbleHandler?.Invoke(incomingByte);

            if (xbeeAPIFrameParsingStarted)
            {
                if (incomingPacketFrameIndex > 0 && incomingPacketFrameIndex <= 2)
                {
                    //Byte 1 and 2 are length bytes
                    //Debug.WriteLine("Parsing Length byte: " + incomingByte.ToString("X2"));
                    incomingPacket.setPacketLengthByte(incomingByte, incomingPacketFrameIndex - 1);
                }
                else if(incomingPacketFrameIndex == 3)
                {
                    //Debug.WriteLine("Parsing Frame Type byte: " + incomingByte.ToString("X2"));
                    incomingPacket.FrameType = XBeeIncomingPacket.toFrameType(incomingByte);
                }
                else if((incomingPacketFrameIndex - 3) <= incomingPacket.PacketDataLength)
                {
                    //Debug.WriteLine("Parsing Packet Data Byte: " + incomingByte.ToString("X2"));
                    incomingPacket.setPacketDataByte(incomingByte, incomingPacketFrameIndex - 4);
                }
                else
                {
                    //Expect checksum byte
                    //Debug.WriteLine("Parsing Checksum Byte: " + incomingByte.ToString("X2"));
                    incomingPacket.verifyChecksum(incomingByte);
                    xbeeIncomingPacketAvaialbleHandler?.Invoke(incomingPacket);

                    incomingPacketFrameIndex = 0;
                    xbeeAPIFrameParsingStarted = false;
                }

                incomingPacketFrameIndex++;

            }
            else if (incomingByte == XBEE_PACKET_START_DELIMITER)
            {
                //Debug.WriteLine("Parsing Start Delimiter Byte: " + incomingByte.ToString("X2"));
                xbeeAPIFrameParsingStarted = true;
                incomingPacket = new XBeeIncomingPacket();
                incomingPacketFrameIndex = 1;
            }
        }
     
        public static void SerialPortDataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            if (!serialPort.IsOpen)
            {
                return;
            }

            int numberOfBytesToRead = serialPort.BytesToRead;

            while(numberOfBytesToRead > 0)
            {
                byte incomingByte = (byte)serialPort.ReadByte();
                IncomingByteHandler(incomingByte);
                numberOfBytesToRead --;
            }
        }

        public void closeSerialPort()
        {
            serialPort.Close();
        }

        public void openSerialPort(String name, int baud)
        {
            if (!serialPort.IsOpen)
            {
                serialPort.PortName = name;
                serialPort.BaudRate = baud;
                serialPort.Open();
            }
        }

        public bool serialPortIsOPen()
        {
            return serialPort.IsOpen;
        }

    }
}