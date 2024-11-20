using Haply.Inverse.Unity;
using UnityEngine;

public class ForceControl : MonoBehaviour
{
    [Range(-2, 2)]
    public float forceX;
    [Range(-2, 2)]
    public float forceY;
    [Range(-2, 2)]
    public float forceZ;

    private Inverse3 _inverse3;

    private void Awake()
    {
        _inverse3 = GetComponentInChildren<Inverse3>();
    }

    protected void OnEnable()
    {
        _inverse3.DeviceStateChanged += OnDeviceStateChanged;
    }

    protected void OnDisable()
    {
        _inverse3.DeviceStateChanged -= OnDeviceStateChanged;
        _inverse3.Release();
    }

    private void OnDeviceStateChanged(Inverse3 inverse3)
    {
        inverse3.CursorSetForce(forceX, forceY, forceZ);
    }
}