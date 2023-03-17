using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
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

        private async void butDownload_Click(object sender, RoutedEventArgs e)
        {
            if (listView.SelectedIndex == -1) return;
            StopWatcher();
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
                Guid uid =  Guid.Parse("0000FFE1-0000-1000-8000-00805F9B34FB"); // Guid характеристики
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
                                var result3 = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
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

                        foreach (GattCharacteristic c in characteristics)
                        {
                            if (c.Uuid == uid)
                            {
                                string url = tbURL.Text;
                                string login = tbLogin.Text;
                                string password = tbPassword.Text;
                                string content = String.Empty;
                                CMD command = 0;
                                if (sender == butDownload_URL)
                                {
                                    content = tbURL.Text;
                                    command = CMD.SetURL;
                                    Settings.URL = content;
                                    Settings.Save();
                                }
                                if (sender == butDownload_Port)
                                {
                                    content = tbPort.Text;
                                    command = CMD.SetPort;
                                    Settings.Port = content;
                                    Settings.Save();
                                }
                                if (sender == butDownload_Login)
                                {
                                    content = tbLogin.Text;
                                    command = CMD.SetLogin;
                                    Settings.Login = content;
                                    Settings.Save();
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
                                if (sender == butDownload_ID)
                                {
                                    content = tbID.Text;
                                    command = CMD.SetID;
                                    Settings.ID = content;
                                    Settings.Save();
                                }
                                if (content == String.Empty) return;
                                try
                                {
                                    List<byte[]> ByteBufferList;
                                    IBuffer writeBuffer;
                                    ByteBufferList = CreateByteBuffers(command, content);
                                    for (int i = 0; i < ByteBufferList.Count; i++)
                                    {
                                        writeBuffer = CryptographicBuffer.CreateFromByteArray(ByteBufferList[i]);
                                        await WriteBufferToSelectedCharacteristicAsync(writeBuffer, c);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    throw new Exception("Ошибка передачи данных" + ex.Message);
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        private List<byte[]> CreateByteBuffers(CMD command, string content)
        {
            const int sendBufSize = 20;
            byte sizeOfString = (byte)content.Length;
            byte sizeOfHeader = 3;
            byte[] ByteBuffer = new byte[Math.Min(sizeOfString + sizeOfHeader + 2, sendBufSize)];
            List<byte[]> result = new List<byte[]>();
            result.Add(ByteBuffer);
            byte index = 0;
            ByteBuffer[index] = (byte)CMD.Marker1;
            index++;
            ByteBuffer[index] = (byte)CMD.Marker2;
            index++;
            ByteBuffer[index] = (byte)command;
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
                }
            }
            ByteBuffer[ByteBuffer.Length - 2] = 0; //признак конца строки
            AddCheckSum(ByteBuffer);
            return result;
        }

        //Добавляет контрольную сумму в последний элемент массива
        private static void AddCheckSum(byte[] dataArray)
        {
            byte sum = 0;
            for (int i = 3; i < dataArray.Length - 1; i++)
            {
                sum += dataArray[i];
            }
            dataArray[dataArray.Length - 1] = sum;
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

        private void listView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (listView.SelectedIndex >= 0)
            {
                butDownload_URL.IsEnabled = true;
                butDownload_Port.IsEnabled = true;
                butDownload_Login.IsEnabled = true;
                butDownload_Password.IsEnabled = true;
                butDownload_Point.IsEnabled = true;
                butDownload_ID.IsEnabled = true;
            }
        }
    }
}
