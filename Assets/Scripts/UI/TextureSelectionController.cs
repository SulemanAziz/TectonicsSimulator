using UnityEngine;
using UnityEngine.UI;

public class TextureSelectionController: MonoBehaviour
{
    public Planet planet;
    public Texture2D texturepointer = null;
    public void ShowPanel()
    {
        gameObject.SetActive(true);
    }
    public void HidePanel(){
        texturepointer = null;
        gameObject.SetActive(false);
    }

    public void SelectTexture(string texturename)
    {
        switch (texturename)
        {
            case "GravityAnomalies":
                texturepointer = Resources.Load<Texture2D>("Bouguer_Gravity_Anomalies_WGM");
                break;  
            case "BWTopo":
                texturepointer = Resources.Load<Texture2D>("TopoHeight");
                break;
            case "NASA-Color":
                texturepointer = Resources.Load<Texture2D>("ColorMap");
                break;
            case "OceanFloor":
                texturepointer = Resources.Load<Texture2D>("Ocean");
                break;
            case "ScoteseCurrent":
                texturepointer = Resources.Load<Texture2D>("Map1");
                break;
            case "ScoteseTopo":
                texturepointer = Resources.Load<Texture2D>("Topography");
                break;    
        }
    }

    public void ApplyChanges()
    {
        if (texturepointer == null)
        {
            gameObject.SetActive(false);
            return;
        }
        
        planet.ChangeColorTexture(texturepointer);
        gameObject.SetActive(false);
    }

}
