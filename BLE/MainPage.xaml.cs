using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x419

namespace BLE
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DeviceWatcher deviceWatcher;
        private ObservableCollection<DeviceInformation> Devices = new ObservableCollection<DeviceInformation>();
        private List<DeviceInformation> UnknownDevices = new List<DeviceInformation>();
        private BluetoothLEDevice bluetoothLEDevice = null;
        private GSMSettings Settings;
        private GattCharacteristic selectedCharacteristic;
        private CMD CurrentCommand;
        private DispatcherTimer timer;
        int SendStep = 0;
        public MainPage()
        {
            this.InitializeComponent();

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;
            ApplicationView.GetForCurrentView().Title = "Тонометр - настройка параметров GSM соединения";
            Settings = new GSMSettings();
            tbURL.Text = Settings.URL;
            tbPort.Text = Settings.Port;
            tbLogin.Text = Settings.Login;
            tbPassword.Text = Settings.Password;
            tbPoint.Text = Settings.Point;
            tbID.Text = Settings.ID;

            timer = new DispatcherTimer() { Interval = new TimeSpan(0, 0, 0, 0, 800) }; 
            timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, object e)
        {
            switch (SendStep)
            {
                case 1:
                    CurrentCommand = CMD.SetURL;
                    SendBuffer(CurrentCommand, tbURL.Text);
                    SendStep++;
                    break;
                case 2:
                    CurrentCommand = CMD.SetPort;
                    SendBuffer(CurrentCommand, tbPort.Text);
                    SendStep++;
                    break;
                case 3:
                    CurrentCommand = CMD.SetLogin;
                    SendBuffer(CurrentCommand, tbLogin.Text);
                    SendStep++;
                    break;
                case 4:
                    CurrentCommand = CMD.SetPassword;
                    SendBuffer(CurrentCommand, tbPassword.Text);
                    SendStep++;
                    break;
                case 5:
                    CurrentCommand = CMD.SetPoint;
                    SendBuffer(CurrentCommand, tbPoint.Text);
                    SendStep++;
                    break;
                case 6:
                    CurrentCommand = CMD.SetID;
                    SendBuffer(CurrentCommand, tbID.Text);
                    SendStep++;
                    break;
                default:
                    SendStep = 0;
                    timer.Stop();
                    break;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            button_Click(null, null);
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (deviceWatcher == null)
            {
                StartWatcher();
                buttonDiscovery.Content = "Остановить";
                NotifyUser($"Поиск устройств...");
            }
            else
            {
                StopWatcher();
                buttonDiscovery.Content = "Поиск устройств";
                NotifyUser($"Поиск остановлен");
            }
        }

        public void NotifyUser(string strMessage)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage));
            }
        }

        private void UpdateStatus(string strMessage)
        {
            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBlock.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBlock.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void StartWatcher()
        {
            Devices.Clear();
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start the watcher.
            deviceWatcher.Start();
        }

        private void StopWatcher()
        {
            if (deviceWatcher != null)
            {
                // Unregister the event handlers.
                deviceWatcher.Added -= DeviceWatcher_Added;
                deviceWatcher.Updated -= DeviceWatcher_Updated;
                deviceWatcher.Removed -= DeviceWatcher_Removed;
                deviceWatcher.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                deviceWatcher.Stopped -= DeviceWatcher_Stopped;

                // Stop the watcher.
                deviceWatcher.Stop();
                deviceWatcher = null;
            }
        }
        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    NotifyUser($"No longer watching for devices.");
                }
            });
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcher)
                {
                    NotifyUser($"Найдено устройств : {Devices.Count}. Поиск завершен.");
                    buttonDiscovery.Content = "Поиск устройств";
                }
            });
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Removed {0}{1}", deviceInfoUpdate.Id, ""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Find the corresponding DeviceInformation in the collection and remove it.
                        DeviceInformation deviceInfo = FindDevice(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            Devices.Remove(deviceInfo);
                        }

                        DeviceInformation deviceInfoUnknown = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfoUnknown != null)
                        {
                            UnknownDevices.Remove(deviceInfoUnknown);
                        }
                    }
                }
            });
        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Updated {0}{1}", deviceInfoUpdate.Id, ""));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        DeviceInformation deviceInfo = FindDevice(deviceInfoUpdate.Id);
                        if (deviceInfo != null)
                        {
                            // Device is already being displayed - update UX.
                            deviceInfo.Update(deviceInfoUpdate);
                            return;
                        }

                        DeviceInformation deviceInfoUnknown = FindUnknownDevices(deviceInfoUpdate.Id);
                        if (deviceInfoUnknown != null)
                        {
                            deviceInfoUnknown.Update(deviceInfoUpdate);
                            // If device has been updated with a friendly name it's no longer unknown.
                            if (deviceInfoUnknown.Name != String.Empty)
                            {
                                Devices.Add(deviceInfoUnknown);
                                UnknownDevices.Remove(deviceInfoUnknown);
                            }
                        }
                    }
                }
            });
        }

        private DeviceInformation FindDevice(string id)
        {
            foreach (var device in Devices)
            {
                if (device.Id == id)
                {
                    return device;
                }
            }
            return null;
        }

        private DeviceInformation FindUnknownDevices(string id)
        {
            foreach (var device in UnknownDevices)
            {
                if (device.Id == id)
                {
                    return device;
                }
            }
            return null;
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    Debug.WriteLine(String.Format("Added {0}{1}", deviceInfo.Id, deviceInfo.Name));

                    // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                    if (sender == deviceWatcher)
                    {
                        // Make sure device isn't already present in the list.
                        if (FindDevice(deviceInfo.Id) == null)
                        {
                            if (deviceInfo.Name != string.Empty)
                            {
                                // If device has a friendly name display it immediately.
                                Devices.Add(deviceInfo);
                            }
                            else
                            {
                                // Add it to a list in case the name gets updated later. 
                                UnknownDevices.Add(deviceInfo);
                            }
                        }

                    }
                }
            });
        }

        private async void SendBuffer(CMD command, string content)
        {
            List<byte[]> ByteBufferList;
            IBuffer writeBuffer;
            CurrentCommand = command;
            ByteBufferList = CreateByteBuffers(CurrentCommand, content);
            for (int i = 0; i < ByteBufferList.Count; i++)
            {
                writeBuffer = CryptographicBuffer.CreateFromByteArray(ByteBufferList[i]);
//                DebugReceiver(ByteBufferList[1], (byte)command);
                await WriteBufferToSelectedCharacteristicAsync(writeBuffer, selectedCharacteristic);
            }
        }
        private void SendCommand(object sender, RoutedEventArgs e)
        {
            string content = String.Empty;
            CurrentCommand = 0;
            SelectCommand(sender, ref CurrentCommand, ref content);

            if (CurrentCommand == 0) return;
            try
            {
                SendBuffer(CurrentCommand, content);
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка передачи данных" + ex.Message);
            }
        }

        byte[] resultBuffer = new byte[200];
        int indexUrl = 0;
        int BLE_PACKET_SIZE = 20;
        int lastPacketNum = 0;
        int alreadyPackets = 0;
        private int DebugReceiver(byte[] input, byte cmd)
        {
            int indexOfStartData = 4;
            int dataSize = 15;

            bool flag = false;
            for (int i = 0; i < BLE_PACKET_SIZE; i++)
            {
                if (input[0] == '0' && input[1] == '2')
                {
                    flag = true;
                }
                if (flag)
                {
                    if (input[i + 2] == cmd)
                    {
                        alreadyPackets++;
                        int numOfPacket = input[i + 3];
                        indexUrl = numOfPacket * dataSize;
                        int index;
                        byte sum = 0;
                        for (index = indexOfStartData; index < BLE_PACKET_SIZE - 1; index++)
                        {
                            resultBuffer[indexUrl] = input[i + index];
                            sum += input[i + index];
                            indexUrl++;
                            if (input[i + index] == 0)
                            {
                                lastPacketNum = numOfPacket;
                                if (input[i + index + 1] == sum)
                                {
                                    return 0;
                                }
                                return 1;
                            }
                        }
                        if (lastPacketNum > 0)
                        {
                            if (alreadyPackets == lastPacketNum + 1)
                            {
                                lastPacketNum = 0;
                                alreadyPackets = 0;
                                var charBuf = resultBuffer.Select(x => (char)x).ToArray();
                                string sss = new string(charBuf);
                                tbDeviceInfo.Text = sss;
                            }
                        }
                        if (input[input.Length - 1] == sum)
                        {
                            return 0;
                        }
                        return 1;
                    }
                }
            }
            return 0;
        }

        //В зависимости от sender выбирается команда и устанавливается content
        private void SelectCommand(object sender, ref CMD command, ref string content)
        {
            if (sender == butDownload_URL)
            {
                content = tbURL.Text;
                command = CMD.SetURL;
                Settings.URL = content;
                Settings.Save();
            }
            if (sender == butRead_URL)
            {
                command = CMD.GetURL;
                content = String.Empty;
            }
            if (sender == butDownload_Port)
            {
                content = tbPort.Text;
                command = CMD.SetPort;
                Settings.Port = content;
                Settings.Save();
            }
            if (sender == butRead_Port)
            {
                command = CMD.GetPort;
                content = String.Empty;
            }
            if (sender == butDownload_Login)
            {
                content = tbLogin.Text;
                command = CMD.SetLogin;
                Settings.Login = content;
                Settings.Save();
            }
            if (sender == butRead_Login)
            {
                command = CMD.GetLogin;
                content = String.Empty;
            }
            if (sender == butDownload_Password)
            {
                content = tbPassword.Text;
                command = CMD.SetPassword;
                Settings.Password = content;
                Settings.Save();
            }
            if (sender == butDownload_Point)
            {
                content = tbPoint.Text;
                command = CMD.SetPoint;
                Settings.Point = content;
                Settings.Save();
            }
            if (sender == butRead_Point)
            {
                command = CMD.GetPoint;
                content = String.Empty;
            }
            if (sender == butDownload_ID)
            {
                content = tbID.Text;
                command = CMD.SetID;
                Settings.ID = content;
                Settings.Save();
            }
            if (sender == butRead_ID)
            {
                command = CMD.GetID;
                content = String.Empty;
            }
        }

        //Создает список 20-байтовых буферов для отправки. В буфере первые 2 байта - маркеры, затем код команды, затем строка, 0 и 
        //контрольная сумма.
        private List<byte[]> CreateByteBuffers(CMD command, string content)
        {
            const int sendBufSize = 20;
            byte sizeOfString = (byte)content.Length;
            byte sizeOfHeader = 4; //Два байта - маркеры, код команды и номер пакета
            byte sizeOfTail = 2; //Хвост - 0 - признак конца строки и контрольная сумма
            byte[] ByteBuffer = new byte[Math.Min(sizeOfString + sizeOfHeader + sizeOfTail, sendBufSize)];
            List<byte[]> result = new List<byte[]>();
            result.Add(ByteBuffer);
            byte index = 0;
            ByteBuffer[index] = (byte)CMD.Marker1;
            index++;
            ByteBuffer[index] = (byte)CMD.Marker2;
            index++;
            ByteBuffer[index] = (byte)command;
            index++;
            ByteBuffer[index] = 0;
            index++;
            for (int i = 0; i < content.Length; i++)
            {
                ByteBuffer[index] = (byte)content[i];
                index++;
                if (index == sendBufSize - 1)
                {
                    AddCheckSum(ByteBuffer);
                    index = 0;
                    ByteBuffer = new byte[sendBufSize];
                    result.Add(ByteBuffer);
                    ByteBuffer[index] = (byte)CMD.Marker1;
                    index++;
                    ByteBuffer[index] = (byte)CMD.Marker2;
                    index++;
                    ByteBuffer[index] = (byte)command;
                    index++;
                    ByteBuffer[index] = (byte)(result.Count - 1);
                    index++;
                }
            }
            ByteBuffer[index] = 0; //признак конца строки
            AddCheckSum(ByteBuffer);
            return result;
        }

        //Добавляет контрольную сумму в последний элемент массива или если встречается 0 - в следующий за ним элемент
        private static void AddCheckSum(byte[] dataArray)
        {
            int indexOfStartData = 4;
            int indexOfZero = dataArray.Length;
            for (int i = indexOfStartData; i < dataArray.Length; i++)
            {
                if (dataArray[i] == 0)
                {
                    indexOfZero = i;
                    break;
                }
            }
            byte sum = 0;
            int lastIndex = Math.Min(dataArray.Length - 1, indexOfZero + 1);
            for (int i = 3; i < lastIndex; i++)
            {
                sum += dataArray[i];
            }
            dataArray[lastIndex] = sum;
        }

        private void ClearBluetoothLEDevice()
        {
            bluetoothLEDevice?.Dispose();
            bluetoothLEDevice = null;
        }

        private async Task<bool> WriteBufferToSelectedCharacteristicAsync(IBuffer buffer, GattCharacteristic c)
        {
            // BT_Code: Writes the value from the buffer to the characteristic.
            var result = await c.WriteValueWithResultAsync(buffer); 
            if (result.Status == GattCommunicationStatus.Success) return true;
            return false;
        }

        private void EnableButtons(bool enable)
        {
            butDownload_URL.IsEnabled = enable;
            butDownload_Port.IsEnabled = enable;
            butDownload_Login.IsEnabled = enable;
            butDownload_Password.IsEnabled = enable;
            butDownload_Point.IsEnabled = enable;
            butDownload_ID.IsEnabled = enable;
            butRead_URL.IsEnabled = enable;
            butRead_Port.IsEnabled = enable;
            butRead_Login.IsEnabled = enable;
            butRead_Point.IsEnabled = enable;
            butRead_ID.IsEnabled = enable;
            butDownloadAll.IsEnabled = enable;
        }

        private async void butConnect_Click(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedIndex == -1) return;
            StopWatcher();
            buttonDiscovery.Content = "Поиск устройств";
            NotifyUser($"Поиск остановлен");
            ClearBluetoothLEDevice();
            DeviceInformation devinf = Devices[listView.SelectedIndex];
            string id = devinf.Id;
            try
            {
                bluetoothLEDevice = await BluetoothLEDevice.FromIdAsync(id);
                if (bluetoothLEDevice == null)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                throw new Exception();
            }
            GattDeviceServicesResult result = await bluetoothLEDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

            if (result.Status == GattCommunicationStatus.Success)
            {
                Guid uuid = Guid.Parse("0000FFE0-0000-1000-8000-00805F9B34FB"); // Guid сервиса
                Guid uid = Guid.Parse("0000FFE1-0000-1000-8000-00805F9B34FB"); // Guid характеристики
                var services = result.Services;

                foreach (var service in services)
                {
                    if (service.Uuid == uuid)
                    {
                        IReadOnlyList<GattCharacteristic> characteristics = null;
                        try
                        {
                            // Ensure we have access to the device.
                            var accessStatus = await service.RequestAccessAsync();
                            if (accessStatus == DeviceAccessStatus.Allowed)
                            {
                                // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                                // and the new Async functions to get the characteristics of unpaired devices as well. 
                                var result3 = await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached);
                                if (result3.Status == GattCommunicationStatus.Success)
                                {
                                    characteristics = result3.Characteristics;
                                }
                                else
                                {
                                    return;
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                        catch (Exception)
                        {
                            return;
                        }

                        selectedCharacteristic = characteristics.FirstOrDefault(x => x.Uuid == uid);
                        if (selectedCharacteristic != null)
                        {
                            await Subscribe();
                            selectedCharacteristic.ValueChanged += SelectedCharacteristic_ValueChanged;

                            tbDeviceInfo.Text = $"Подключено: {bluetoothLEDevice.BluetoothAddress}, {DisplayHelpers.GetCharacteristicName(selectedCharacteristic)}";
                            EnableButtons(true);
                        }
                    }
                }
            }
        }

        private async Task<bool> Subscribe()
        {
            GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
            var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;
            if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
            {
                cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            }

            else if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
            {
                cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
            }

            try
            {
                // BT_Code: Must write the CCCD in order for server to send indications.
                // We receive them in the ValueChanged event handler.
                status = await selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                if (status == GattCommunicationStatus.Success)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return false;
            }

        }


        private async void SelectedCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var value = args.CharacteristicValue;
            byte[] buff = new byte[value.Capacity];
            for (uint i = 0; i < value.Capacity; i++)
            {
                buff[i] = value.GetByte(i);
            }
            var carr = buff.Select(x => (Char)(x)).ToArray();
            string s = new string(carr);

            await Dispatcher.RunAsync(
            Windows.UI.Core.CoreDispatcherPriority.Normal,
            () => {
                CurrentTextBox().Text = s;
            });            
        }

        private TextBox CurrentTextBox()
        {
            switch (CurrentCommand)
            {
                case CMD.GetURL: return tbURL;
                case CMD.GetPort: return tbPort;
                case CMD.GetLogin: return tbLogin;
                case CMD.GetPoint: return tbPoint;
                case CMD.GetID: return tbID;
                default: return null;
            }
        }

        private void butDownloadAll_Click(object sender, RoutedEventArgs e)
        {
            timer.Start();
            SendStep = 1;
        }

        private void listView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            butConnect.IsEnabled = true;
        }
    }
}
