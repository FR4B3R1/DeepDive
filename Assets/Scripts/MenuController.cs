using UnityEngine;
using UnityEngine.EventSystems;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject firstButton;

    private void OnEnable()
    {
        EventSystem.current.SetSelectedGameObject(null); // reset
        EventSystem.current.SetSelectedGameObject(firstButton);
    }
}
