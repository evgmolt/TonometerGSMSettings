using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLE
{
    internal enum CMD
    {
        Marker1 = 0x30,
        Marker2 = 0x32,
        SetTime     = 3,
        SetURL      = 6,
        SetPort     = 7,
        SetLogin    = 8,
        SetPassword = 9,
        SetPoint    = 10,
        SetID       = 11,
        GetURL      = 12,
        GetPort     = 13,
        GetPoint    = 14,
        GetLogin    = 15,
        GetPassword = 16,
        GetID       = 17,
    }
}
