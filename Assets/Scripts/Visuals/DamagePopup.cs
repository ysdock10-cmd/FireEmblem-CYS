using System.Collections;
using UnityEngine;

namespace SRPG
{
    public class DamagePopup : MonoBehaviour
    {
        private const float RiseDistance = 0.7f;
        private const float Duration = 0.8f;
        private const float HoldFraction = 0.55f;

        public static void Spawn(Vector3 worldPos, int amount)
        {
            var color = amount > 0 ? new Color(1f, 0.9f, 0.2f) : new Color(0.8f, 0.8f, 0.8f);
            SpawnText(worldPos, amount.ToString(), color, 0.13f);
        }

        private static void SpawnText(Vector3 worldPos, string text, Color color, float characterSize)
        {
            var go = new GameObject("DamagePopup");
            go.transform.position = worldPos + new Vector3(0f, 0.45f, 0f);
            go.AddComponent<DamagePopup>().Show(text, color, characterSize);
        }

        private void Show(string text, Color color, float characterSize)
        {
            var tm = gameObject.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = characterSize;
            tm.fontSize = 80;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            // 이미 두꺼운 제목용 폰트라 FontStyle.Bold(인위적 굵기)는 겹쳐 더 뭉개지므로 뺌
            tm.font = Resources.Load<Font>("Fonts/BlackHanSans-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var mr = GetComponent<MeshRenderer>();
            mr.material = tm.font.material;
            mr.sortingOrder = 30;

            StartCoroutine(Animate(tm));
        }

        private IEnumerator Animate(TextMesh tm)
        {
            Vector3 start = transform.position;
            Vector3 end = start + new Vector3(0f, RiseDistance, 0f);
            Color baseColor = tm.color;
            float t = 0f;

            while (t < Duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / Duration);
                transform.position = Vector3.Lerp(start, end, p);
                float alpha = p < HoldFraction ? 1f : Mathf.Lerp(1f, 0f, (p - HoldFraction) / (1f - HoldFraction));
                tm.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
