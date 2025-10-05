//Controls movement of collectors

using System;
using UnityEngine;

public class CollectorMovement : MonoBehaviour
{
   
   [SerializeField] private float collectorLinearVelocity = 2f; //how many units per second

   private bool _isDisabled = false;

   private void Update()
   {
      RegularMoveForward();
   }

   private void RegularMoveForward()
   {
      if (!_isDisabled)
      {
         transform.Translate(Vector3.forward * collectorLinearVelocity * Time.deltaTime, Space.World);
      }
   }


   private void setDisable()
   {
      _isDisabled = true;
   }


   public void GradualStop()
   {
      
   }

   public void VeerLeft()
   {
      
   }

   public void VeerRight()
   {
      
   }
}
