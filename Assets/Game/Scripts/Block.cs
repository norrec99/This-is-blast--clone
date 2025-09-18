using UnityEngine;

public enum BlockColor { Red, Green, Blue, Yellow, Purple }

public class Block : MonoBehaviour
{
    public int X;
    public int Y;
    public BlockColor Color;

    private MeshRenderer rend;

    void Awake()
    {
        rend = GetComponent<MeshRenderer>();
    }

    public void Init(int x, int y, BlockColor color, Material[] colorMats)
    {
        X = x; Y = y; Color = color;
        transform.localRotation = Quaternion.Euler(0, 0, 0);
        if (rend == null)
        {
            rend = GetComponent<MeshRenderer>();
        }
        rend.material = colorMats[(int)color];
    }

    public void SetCoords(int x, int y)
    {
        X = x; Y = y;
    }
}
