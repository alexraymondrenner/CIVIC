using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActionManager : MonoBehaviour
{

    [SerializeField]
    private GameObject Submit, Wait,Confirm,TempelateA;

    [SerializeField, ReadOnly]
    private GameObject ActiveIcon;

    // Start is called before the first frame update
    void Start()
    {
        TurnOffAll();

    }

    public void SubmitAction()
    {
        if (ActiveIcon == null)
            return;

        GameObject managerObject =
            GameObject.Find("GameManager");

        if (managerObject == null)
            return;

        ResourceArrivalManager arrivalManager =
            managerObject.GetComponent<ResourceArrivalManager>();

        if (arrivalManager == null)
            return;

        arrivalManager.RequestResolveInjuct(
            ActiveIcon.name
        );
        TurnOffAll();
    }
    public void SetActiveIcon(GameObject curentIcon) { ActiveIcon = curentIcon; }

    public void TurnOffAll()
    {
        foreach (Transform i in this.transform)
        {
            i.gameObject.SetActive(false);
        }
    }


    public void SelectOpA() {

        TurnOffAll();
        TurnOnSubmit();
    }

    public void SelectOpB() { 
        TurnOffAll(); 
    }
    
    public void SelectOpC() { 
        TurnOffAll(); 
    }

    public void TurnOnTempelate() {
        TurnOffAll();
        //Turn on Tempelate, then based on icon type need to decide which tempelate
        TempelateA.SetActive(true);
        Toggle[] Toggles = TempelateA.GetComponentsInChildren<Toggle>();
        foreach (Toggle i in Toggles) { i.isOn = false;}
        
    }


    public void TurnOnConfirm()
    {

        TurnOffAll();
        Confirm.SetActive(true);
    }

    public void TurnOnSubmit()
    {
       // TurnOffAll();

        Submit.SetActive(true);
    }
    public void TurnOnWait()
    {
        TurnOffAll();
        Wait.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
