using UnityEngine;

// Loop Games
// pooL semaG
public class NewMonoBehaviourScript : MonoBehaviour
{

public void Awake(){
    MakeReverse("Loop Games");
}
public void MakeReverse(String _sentence)
{
    String[] new_sentence;
    foreach (var word in _sentence)
    {   
        for(int i = word.size(); i > 0; i--)
        {
           new_sentence.add(word[i]); 
        }
        new_sentence.add(" ");   
    }
    Debug.log(new_sentence);
}

}
