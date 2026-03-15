using UnityEngine;
using System.Collections;

public class 自旋转 : MonoBehaviour
{
    public float 旋转速度 = 0.01f;  //控制速度
    public bool 同步旋转 = false;
    public Rigidbody m_RB;

    public bool m_物理运动 = false;

    void Start ()
    {
	
	}
	
	// Update is called once per frame
	void Update ()
    {
        if(同步旋转)
        {
            gameObject.transform.Rotate(new Vector3(旋转速度, 旋转速度, 旋转速度), Space.World);
        }
        else
        {
            gameObject.transform.Rotate(new Vector3(0, 旋转速度, 0), Space.World);
        }

        //if(m_物理运动)
        //{
        //    m_RB.MovePosition(gameObject.transform.position - gameObject.transform.forward * 1 * Time.deltaTime);

            
        //}
        //else
        //{
        //    gameObject.transform.position = gameObject.transform.position - gameObject.transform.forward * 1 * Time.deltaTime;
        //}

    }
}
