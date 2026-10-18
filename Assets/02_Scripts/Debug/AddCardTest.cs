using System;
using SushiDojo.Cards;
using SushiDojo.Ingredients;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiDojo.DebugTools
{
    public class AddCardTest : MonoBehaviour
    {
        [SerializeField] IngredientHandView hand;
        [SerializeField] IngredientData[] ingredientPool = Array.Empty<IngredientData>();
        [SerializeField] Vector2 startPosition;
        [SerializeField] Key addCardKey = Key.K;

        void Awake()
        {
            if (hand == null || ingredientPool == null || ingredientPool.Length == 0)
            {
                Debug.LogError(
                    $"{nameof(AddCardTest)} requires an ingredient hand and a non-empty ingredient pool.",
                    this);
                enabled = false;
                return;
            }

            for (int i = 0; i < ingredientPool.Length; i++)
            {
                if (ingredientPool[i] != null) continue;

                Debug.LogError(
                    $"{nameof(AddCardTest)} has an empty ingredient pool entry at index {i}.",
                    this);
                enabled = false;
                return;
            }
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[addCardKey].wasPressedThisFrame) return;

            IngredientData ingredient = ingredientPool[UnityEngine.Random.Range(0, ingredientPool.Length)];
            hand.AddCard(ingredient, startPosition);
            Debug.Log($"[AddCardTest] Added {ingredient.DisplayName} to the hand.", this);
        }
    }
}
