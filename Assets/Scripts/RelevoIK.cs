using UnityEngine;

/// <summary>
/// Unity solo llama a OnAnimatorIK en el objeto que tiene el Animator. DefensaOponente vive en la raíz del oponente
/// y añade este componente al Animator en tiempo de ejecución para recibir la llamada.
/// </summary>
public class RelevoIK : MonoBehaviour
{
    public DefensaOponente Destino { get; set; }

    private void OnAnimatorIK(int capa)
    {
        if (Destino != null) Destino.AplicarIK(capa);
    }
}
