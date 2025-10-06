using UnityEngine;

// Place on a trigger volume or call Win() manually when your win condition is met.
public class LevelGoal : MonoBehaviour
{
    [SerializeField] private string collectorTag = "Collector";
    [SerializeField] private bool triggerOnCollectorEnter = true;

    public void Lose()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoseLevel();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnCollectorEnter) return;
        if (other.CompareTag(collectorTag))
        {
            Lose();
        }
    }
}


