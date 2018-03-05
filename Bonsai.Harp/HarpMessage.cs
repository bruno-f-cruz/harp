﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Harp
{
    public class HarpMessage
    {
        const byte ErrorMask = 0x08;

        public HarpMessage(params byte[] messageBytes)
        {
            if (messageBytes == null)
            {
                throw new ArgumentNullException("messageBytes");
            }

            MessageBytes = messageBytes;
        }

        public HarpMessage(bool updateChecksum, params byte[] messageBytes)
            : this(messageBytes)
        {
            if (updateChecksum)
            {
                messageBytes[messageBytes.Length - 1] = GetChecksum(messageBytes, messageBytes.Length - 1);
            }
        }

        public MessageType MessageType
        {
            get { return (MessageType)(MessageBytes[0] & ~ErrorMask); }
        }

        public int Address
        {
            get { return MessageBytes[2]; }
        }

        public int Port
        {
            get { return MessageBytes[3]; }
        }

        public PayloadType PayloadType
        {
            get { return (PayloadType)MessageBytes[4]; }
        }

        public bool Error
        {
            get { return (MessageBytes[0] & ErrorMask) != 0; }
        }

        public bool IsTimestamped
        {
            get { return (PayloadType & PayloadType.Timestamp) == PayloadType.Timestamp; }
        }

        public bool IsValid
        {
            get
            {
                var messageId = MessageType;
                var payloadType = PayloadType;
                var sizeOfType = (int)payloadType & 0x0F;
                var payloadArrayLength = (MessageBytes.Length - 10) / sizeOfType;

                if ((messageId != MessageType.Write) &&
                    (messageId != MessageType.Read) &&
                    (messageId != MessageType.Event) &&
                    ((byte)messageId != ((byte)MessageType.Write | ErrorMask)) &&
                    ((byte)messageId != ((byte)MessageType.Read | ErrorMask)))
                {
                    return false;
                }

                /* Check if the size of type is correct */
                if ((sizeOfType != 1) && (sizeOfType != 2) && (sizeOfType != 4) && (sizeOfType != 8))
                {
                    return false;
                }

                /* Check if the payload length is an integer number */
                if ((payloadArrayLength % 1) != 0)
                {
                    return false;
                }

                /* Bit 0x20 can't be high */
                if (((int)payloadType & 0x20) == 0x20)
                {
                    return false;
                }
                
                var checksum = (byte)0;
                for (int i = 0; i < MessageBytes.Length - 1; i++)
                {
                    checksum += MessageBytes[i];
                }
                if (checksum != MessageBytes[MessageBytes.Length - 1])
                {
                    return false;
                }

                return true;
            }
        }

        public byte[] MessageBytes { get; private set; }

        public double GetTimestamp()
        {
            double timestamp;
            if (!TryGetTimestamp(out timestamp))
            {
                throw new InvalidOperationException("This Harp message does not have a timestamped payload.");
            }

            return timestamp;
        }

        public bool TryGetTimestamp(out double timestamp)
        {
            if (IsTimestamped)
            {
                var seconds = BitConverter.ToUInt32(MessageBytes, 5);
                var microseconds = BitConverter.ToUInt16(MessageBytes, 5 + 4);
                timestamp = seconds + microseconds * 32e-6;
                return true;
            }
            else
            {
                timestamp = default(double);
                return false;
            }
        }

        public byte GetChecksum()
        {
            return GetChecksum(MessageBytes, MessageBytes.Length - 1);
        }

        static byte GetChecksum(byte[] messageBytes, int count)
        {
            var checksum = (byte)0;
            for (int i = 0; i < messageBytes.Length; i++)
            {
                checksum += messageBytes[i];
            }
            return checksum;
        }
    }
}