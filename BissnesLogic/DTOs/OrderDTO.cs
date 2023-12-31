﻿using BissnesLogic.Entites;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BissnesLogic.DTOs
{
    public class OrderDTO
    {
        public decimal TotalPrice { get; set; }
        public DateTime Date { get; set; }
        public string UserId { get; set; }
        public string StartPoint { get; set; }
        public string EndPoint { get; set; }
        public int Rating { get; set; }
        public string Type { get; set; }
    }
}
