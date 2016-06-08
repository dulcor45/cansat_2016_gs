﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CanSatGroundStation
{
    class CommandSender
    {

        private byte COMMAND_PACKET_TYPE = 0xB0;

        private byte COMMAND_BYTE_TAKE_IMAGE = 0xC1;
        private byte COMMAND_BYTE_RESET_CAMERA = 0xC2;
        private byte COMMAND_BYTE_DEPLOY_PAYLOAD = 0xC3;

        private static volatile CommandSender commandSenderInstance;

        public static CommandSender Instance
        {
            get
            {

                if (commandSenderInstance == null)
                {
                    commandSenderInstance = new CommandSender();
                }

                return commandSenderInstance;
            }
        }


        public void sendTakePictureCommand()
        {
            XBeeOutgoingPacket commandPacket = new XBeeOutgoingPacket();
            commandPacket.PacketData = new byte[] { COMMAND_PACKET_TYPE, COMMAND_BYTE_TAKE_IMAGE };
            Debug.WriteLine("Take picture command sent: " + BitConverter.ToString(commandPacket.toByteArray()));
            XBee.Instance.sendOutGoingPacket(commandPacket);
        }

        public void sendResetCameraCommand()
        {
            XBeeOutgoingPacket commandPacket = new XBeeOutgoingPacket();
            commandPacket.PacketData = new byte[] { COMMAND_PACKET_TYPE, COMMAND_BYTE_RESET_CAMERA };
            Debug.WriteLine("Reset camera command sent: " + BitConverter.ToString(commandPacket.toByteArray()));
            XBee.Instance.sendOutGoingPacket(commandPacket);
        }

        public void sendDeployPayloadCommand()
        {
            XBeeOutgoingPacket commandPacket = new XBeeOutgoingPacket();
            commandPacket.PacketData = new byte[] { COMMAND_PACKET_TYPE, COMMAND_BYTE_DEPLOY_PAYLOAD };
            Debug.WriteLine("Reset camera command sent: " + BitConverter.ToString(commandPacket.toByteArray()));
            XBee.Instance.sendOutGoingPacket(commandPacket);
        }

       

    }
}
