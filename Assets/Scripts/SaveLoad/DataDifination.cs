using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataDifination : MonoBehaviour
{
    public PresistentType presistentType;
    public string ID;

    private void OnValidate()
    {
        if (presistentType == PresistentType.ReadAndWrite)
        {
            if (ID == string.Empty)
            {
                ID = System.Guid.NewGuid().ToString();
            }
        }
        else
        {
            ID = string.Empty;
        }
    }
}
