using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class PlateMovementController: MonoBehaviour
{
    public GameObject MovementControls;
    public Planet planet;
    public Slider TimeSlider;
    public TextMeshProUGUI timelabel;
    bool controlsvisible = false;

    public void UpdateLabel()
    {
        timelabel.text =  Mathf.RoundToInt(TimeSlider.value).ToString() + " Mya";
    }
    public void UpdateBoundaries()
    {
        if(planet.ShowPlates==true){
        int timeStep = (int) TimeSlider.value;
        planet.ApplyTimeStep(timeStep);
        } else Debug.Log("Plates must be visible to apply changes!");
    }

    public void ToggleControls()
    {
        if(planet.ShowPlates == true)
        {
            if (!controlsvisible)
            {
                MovementControls.SetActive(true);
                controlsvisible = true;
            } 
            else
            {
                MovementControls.SetActive(false);
                controlsvisible = false;   
            }
        } else Debug.Log("Plates must be active to show movement controls!");
        
    }

}