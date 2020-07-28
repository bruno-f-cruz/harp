﻿using System;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive;
using System.Text;

namespace Bonsai.Harp
{
    [Description("Produces a sequence of messages from the Harp device connected at the specified serial port.")]
    public class Device : Source<HarpMessage>, INamedElement
    {
        string name;
        string portName;
        LedState ledState;
        LedState visualIndicators;

        public Device()
        {
            PortName = "COMx";
            DeviceState = DeviceState.Active;
            LedState = LedState.On;
            VisualIndicators = LedState.On;
            DumpRegisters = true;
            Heartbeat = EnableType.Disable;
            CommandReplies = EnableType.Enable;
        }

        [TypeConverter(typeof(PortNameConverter))]
        [Description("The name of the serial port used to communicate with the Harp device.")]
        public string PortName
        {
            get { return portName; }
            set
            {
                portName = value;
                GetDeviceName(portName, ledState, visualIndicators, Heartbeat).Subscribe(deviceName => name = deviceName);
            }
        }

        [Description("Specifies the state of the device at run time.")]
        public DeviceState DeviceState { get; set; }

        [Description("Specifies whether the device should send the content of all registers during initialization.")]
        public bool DumpRegisters { get; set; }

        [Description("Specifies the state of the device LED.")]
        public LedState LedState
        {
            get { return ledState; }

            set
            {
                ledState = value;
                GetDeviceName(portName, ledState, visualIndicators, Heartbeat).Subscribe(deviceName => name = deviceName);
            }
        }

        [Description("Specifies the state of all the visual indicators in the device.")]
        public LedState VisualIndicators
        {
            get { return visualIndicators; }
            set
            {
                visualIndicators = value;
                GetDeviceName(portName, ledState, visualIndicators, Heartbeat).Subscribe(deviceName => name = deviceName);
            }
        }

        [Description("Specifies if the Device sends the Timestamp Event each second.")]
        public EnableType Heartbeat { get; set; }

        [Description("Specifies if the Device replies to commands.")]
        EnableType CommandReplies { get; set; }

        [Description("Specifies whether error messages parsed during acquisition should be ignored or create an exception.")]
        public bool IgnoreErrors { get; set; }        

        static HarpMessage CreateOperationControl(DeviceState stateMode, LedState ledState, LedState visualIndicators, EnableType heartbeat, EnableType replies, bool dumpRegisters)
        {
            int operationFlags;
            operationFlags  = (heartbeat == EnableType.Enable)  ? 0x80 : 0x00;
            operationFlags += (ledState == LedState.On)         ? 0x40 : 0x00;
            operationFlags += (visualIndicators == LedState.On) ? 0x20 : 0x00;
            operationFlags += (replies == EnableType.Enable)    ? 0x00 : 0x10;
            operationFlags += dumpRegisters                     ? 0x08 : 0x00;
            operationFlags += (stateMode == DeviceState.Active) ? 0x01 : 0x00;
            return HarpMessage.FromByte(Registers.OperationControl, MessageType.Write, (byte)operationFlags);
        }

        static IObservable<string> GetDeviceName(string portName, LedState ledState, LedState visualIndicators, EnableType heartbeat)
        {
            return Observable.Create<string>(observer =>
            {
                var transport = default(SerialTransport);
                var writeOpCtrl = CreateOperationControl(DeviceState.Standby, ledState, visualIndicators, heartbeat, EnableType.Enable, false);
                var cmdReadWhoAmI = HarpMessage.FromUInt16(Registers.WhoAmI, MessageType.Read);
                var cmdReadMajorHardwareVersion = HarpMessage.FromByte(Registers.HardwareVersionHigh, MessageType.Read);
                var cmdReadMinorHardwareVersion = HarpMessage.FromByte(Registers.HardwareVersionLow, MessageType.Read);
                var cmdReadMajorFirmwareVersion = HarpMessage.FromByte(Registers.FirmwareVersionHigh, MessageType.Read);
                var cmdReadMinorFirmwareVersion = HarpMessage.FromByte(Registers.FirmwareVersionLow, MessageType.Read);
                var cmdReadTimestampSeconds = HarpMessage.FromUInt32(Registers.TimestampSecond, MessageType.Read);
                var cmdReadDeviceName = HarpMessage.FromByte(Registers.DeviceName, MessageType.Read);

                var whoAmI = 0;
                var timestamp = 0u;
                var hardwareVersionHigh = 0;
                var hardwareVersionLow = 0;
                var firmwareVersionHigh = 0;
                var firmwareVersionLow = 0;
                var messageObserver = Observer.Create<HarpMessage>(
                    message =>
                    {
                        switch (message.Address)
                        {
                            case Registers.OperationControl:
                                transport.Write(cmdReadWhoAmI);
                                transport.Write(cmdReadMajorHardwareVersion);
                                transport.Write(cmdReadMinorHardwareVersion);
                                transport.Write(cmdReadMajorFirmwareVersion);
                                transport.Write(cmdReadMinorFirmwareVersion);
                                transport.Write(cmdReadTimestampSeconds);
                                transport.Write(cmdReadDeviceName);
                                break;
                            case Registers.WhoAmI: whoAmI = message.GetPayloadUInt16(); break;
                            case Registers.HardwareVersionHigh: hardwareVersionHigh = message.GetPayloadByte(); break;
                            case Registers.HardwareVersionLow: hardwareVersionLow = message.GetPayloadByte(); break;
                            case Registers.FirmwareVersionHigh: firmwareVersionHigh = message.GetPayloadByte(); break;
                            case Registers.FirmwareVersionLow: firmwareVersionLow = message.GetPayloadByte(); break;
                            case Registers.TimestampSecond: timestamp = message.GetPayloadUInt32(); break;
                            case Registers.DeviceName:
                                var deviceName = nameof(Device);
                                if (!message.Error)
                                {
                                    var namePayload = message.GetPayload();
                                    deviceName = Encoding.ASCII.GetString(namePayload.Array, namePayload.Offset, namePayload.Count);
                                }
                                Console.WriteLine("Serial Harp device.");
                                Console.WriteLine($"WhoAmI: {whoAmI}");
                                Console.WriteLine($"Hw: {hardwareVersionHigh}.{hardwareVersionLow}");
                                Console.WriteLine($"Fw: {firmwareVersionHigh}.{firmwareVersionLow}");
                                Console.WriteLine($"Timestamp (s): {timestamp}");
                                Console.WriteLine($"DeviceName: {deviceName}");
                                observer.OnNext(deviceName);
                                observer.OnCompleted();
                                break;
                            default:
                                break;
                        }
                    },
                    observer.OnError,
                    observer.OnCompleted);
                transport = new SerialTransport(portName, messageObserver);
                transport.IgnoreErrors = true;
                transport.Open();

                transport.Write(writeOpCtrl);
                return transport;
            }).Timeout(TimeSpan.FromMilliseconds(500))
              .OnErrorResumeNext(Observable.Return(nameof(Device)))
              .FirstAsync();
        }

        public override IObservable<HarpMessage> Generate()
        {
            return Observable.Create<HarpMessage>(observer =>
            {
                var transport = new SerialTransport(PortName, observer);
                transport.IgnoreErrors = IgnoreErrors;
                transport.Open();
                
                var writeOpCtrl = CreateOperationControl(DeviceState, ledState, visualIndicators, Heartbeat, CommandReplies, DumpRegisters);
                transport.Write(writeOpCtrl);

                var cleanup = Disposable.Create(() =>
                {
                    writeOpCtrl = CreateOperationControl(DeviceState.Standby, ledState, visualIndicators, Heartbeat, CommandReplies, false);
                    transport.Write(writeOpCtrl);
                });

                return new CompositeDisposable(
                    cleanup,
                    transport);
            });
        }

        public IObservable<HarpMessage> Generate(IObservable<HarpMessage> source)
        {
            return Observable.Create<HarpMessage>(observer =>
            {
                var transport = new SerialTransport(PortName, observer);
                transport.IgnoreErrors = IgnoreErrors;
                transport.Open();

                var writeOpCtrl = CreateOperationControl(DeviceState, ledState, visualIndicators, Heartbeat, CommandReplies, DumpRegisters);
                transport.Write(writeOpCtrl);

                var sourceDisposable = new SingleAssignmentDisposable();
                sourceDisposable.Disposable = source.Subscribe(
                    transport.Write,
                    observer.OnError,
                    observer.OnCompleted);

                var cleanup = Disposable.Create(() =>
                {
                    writeOpCtrl = CreateOperationControl(DeviceState.Standby, ledState, visualIndicators, Heartbeat, CommandReplies, false);
                    transport.Write(writeOpCtrl);
                });

                return new CompositeDisposable(
                    cleanup,
                    sourceDisposable,
                    transport);
            });
        }

        string INamedElement.Name => !string.IsNullOrEmpty(name) ? name : nameof(Device);
    }
}
