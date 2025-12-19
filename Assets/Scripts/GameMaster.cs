using Fusion;
using UnityEngine;

public class GameMaster : MonoBehaviour
{
    private NetworkRunner runner;

    void Start()
    {
        runner = FindObjectOfType<NetworkRunner>();
        
        if (runner == null)
        {
            Debug.LogError("NetworkRunner not found!");
            return;
        }

        if (runner.IsServer)
        {
            PickRandomTurn();
        }
    }

    void PickRandomTurn()
    {
        int randomNumber = Random.Range(1, 3);
        
        Debug.Log($"Random number picked: {randomNumber}");

        if (randomNumber == 1)
        {
            Debug.Log("It's the HOST's turn!");
        }
        else
        {
            Debug.Log("It's the CLIENT's turn!");
        }
    }
}