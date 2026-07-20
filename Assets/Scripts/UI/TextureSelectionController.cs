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
                texturepointer = Resources.Load<Texture2D>("BaseMaps/Bouguer_Gravity_Anomalies_WGM");
                break;  
            case "BWTopo":
                texturepointer = Resources.Load<Texture2D>("BaseMaps/TopoHeight");
                break;
            case "NASA-Color":
                texturepointer = Resources.Load<Texture2D>("BaseMaps/ColorMap");
                break;
            case "OceanFloor":
                texturepointer = Resources.Load<Texture2D>("BaseMaps/Ocean");
                break;
            case "ScoteseCurrent":
                texturepointer = Resources.Load<Texture2D>("BaseMaps/Map1");
                break;
            case "ScoteseTopo":
                texturepointer = Resources.Load<Texture2D>("BaseMaps/Topography");
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
