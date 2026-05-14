using ECommons.DalamudServices;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Numerics;
using Lumina.Excel.Sheets;

namespace Teleport
{
    public class TPList
    {
        public List<TP> TPs { get; set; }
        public uint MapId { get; set; }
        public string MapName { get; set; }
        public bool UseQuickWindow { get; set; }
        public TPList(uint mapId)
        {
            TPs = new List<TP>();
            MapId = mapId;
            MapName = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(mapId).PlaceName.Value.Name.ToString();
            UseQuickWindow = false;
        }
        // 序列化自身
        public string ToJson() => JsonConvert.SerializeObject(this);

        // 反序列化
        public static TPList FromJson(string json) => JsonConvert.DeserializeObject<TPList>(json);
    }
    public class TP
    {
        public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public TP()
        {
            Name = "空";
            X = 0;
            Y = 0;
            Z = 0;
        }

        public TP(string name, Vector3 pos)
        {
            Name = name;
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;
        }
        // 获取自身pos

        public Vector3 GetPos() => new Vector3(X, Y, Z);
    }
}
