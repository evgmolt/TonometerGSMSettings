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
        SetLogin = 6,
        GetLogin = 7,
        SetPassword = 8,
        GetPassword = 9,
        SetURL = 10,
        GetURL = 11,
        SetPort = 12,
        GetPort = 13,
        SetPoint = 14,
        GetPoint = 15,
        SetID = 16,
        GetID = 17,
    }
}
