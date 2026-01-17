using UnityEngine;

public class ClickDebugger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("=== НАЧАЛО ОТЛАДКИ КЛИКА ===");

            // 1. Проверяем стандартное OnMouseDown
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D[] allHits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);

            Debug.Log($"Найдено объектов: {allHits.Length}");
            foreach (RaycastHit2D hit in allHits)
            {
                GameObject go = hit.collider.gameObject;
                Debug.Log($"- {go.name} (слой: {LayerMask.LayerToName(go.layer)})");

                // Проверяем компоненты
                NetworkNode node = go.GetComponent<NetworkNode>();
                if (node != null) Debug.Log($"  Содержит NetworkNode!");

                Tower tower = go.GetComponent<Tower>();
                if (tower != null) Debug.Log($"  Содержит Tower!");

                // Проверяем родителей
                NetworkNode parentNode = go.GetComponentInParent<NetworkNode>();
                if (parentNode != null) Debug.Log($"  Родитель: {parentNode.name}");
            }

            Debug.Log("=== КОНЕЦ ОТЛАДКИ ===");
        }
    }
}