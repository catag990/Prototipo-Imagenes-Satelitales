using UnityEngine;
using Unity.Netcode;

public enum MarkerType { POI, Lasso }
public enum MarkerTag { Riesgo, Agua, Calor, Generico }

[System.Serializable]
public struct GeoMarkerData : INetworkSerializable, System.IEquatable<GeoMarkerData>
{
    public ulong markerID;     
    public MarkerType type;
    public MarkerTag tag;
    public Color color;
    public bool isVisible;

    // --- Datos Espaciales para POI ---
    public Vector3 position;
    public Vector3 normal;

    // --- Datos Espaciales para Lazo (Flat Buffer) ---
    public int lassoStartIndex;
    public int lassoPointCount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref markerID);
        serializer.SerializeValue(ref type);
        serializer.SerializeValue(ref tag);
        serializer.SerializeValue(ref color);
        serializer.SerializeValue(ref isVisible);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref normal);
        serializer.SerializeValue(ref lassoStartIndex);
        serializer.SerializeValue(ref lassoPointCount);
    }

    public bool Equals(GeoMarkerData other) => markerID == other.markerID;
}