using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace BLE
{
    internal class GSMSettings
    {
        internal string URL;
        internal string Port;
        internal string Login;
        internal string Password;
        internal string Point;
        internal string ID;

        public GSMSettings()
        {
            ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            // Save a setting locally on the device
            localSettings.Values["setting"] = "a device specific setting";

            // Save a composite setting locally on the device
            Windows.Storage.ApplicationDataCompositeValue composite = (ApplicationDataCompositeValue)localSettings.Values["Settings"];

            try
            {
                if (composite.Count() > 0)
                {
                    URL = composite["URL"] as string;
                    Port = composite["Port"] as string;
                    Login = composite["Login"] as string;
                    Password = composite["Password"] as string;
                    Point = composite["Point"] as string;
                    ID = composite["ID"] as string;
                }
                else
                {
                    SetDefault();
                }
            }
            catch (Exception)
            {
                SetDefault();
            }
        }

        internal void Save()
        {
            ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            // Save a setting locally on the device
            localSettings.Values["test setting"] = "a device specific setting";

            // Save a composite setting locally on the device
            Windows.Storage.ApplicationDataCompositeValue composite = new Windows.Storage.ApplicationDataCompositeValue();
            composite["URL"] = URL;
            composite["Port"] = Port;
            composite["Login"] = Login;
            composite["Password"] = Password;
            composite["Point"] = Point;
            composite["ID"] = ID;

            localSettings.Values["Settings"] = composite;
        }
        private void SetDefault()
        {
            URL = "http://bp-technopark.mdapp.online";
            Port = "80";
            Login = "Login";
            Password = "Rassword";
        }
    }
}
