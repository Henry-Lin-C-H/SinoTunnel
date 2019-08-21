﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinoTunnel
{
    class DuctileCastIron
    {
        public double Rout;  // m
        public double width;
        public double UW; //kN/m²
        public double t1; // cm
        public double t2; // cm
        public double t3; // cm
        public double t; // cm
        public double theta;
        public double Fy; // kg/cm²
        public double E; // kN/m²
        public double Fat; // kg/cm² - 容許抗彎拉應力
        public double Fac; // kg/cm² - 容許抗彎壓應力
        public double Fas; // kg/cm² - 容許抗剪應力
    }

    class GeneralCalculation
    {
        string sectionUID;
        GetWebData p;
        DuctileCastIron sg = new DuctileCastIron();
        STN_VerticalStress oSTN_VerticalStress;

        public GeneralCalculation(string sectionUID, string condition)
        {
            this.sectionUID = sectionUID;
            this.p = new GetWebData(sectionUID);
            this.oSTN_VerticalStress = new STN_VerticalStress(sectionUID, "WEBFORM");
            sg.Rout = p.segmentRadiusOut;
            sg.width = p.segmentWidth * 100; // cm
            sg.UW = 71.05; //kN/m²
            sg.t1 = 2.8; // cm
            sg.t2 = 2.8; // cm
            sg.t3 = 2.8; // cm
            sg.t = 0.2 * 100; // cm
            sg.theta = 7.2; // theta
            sg.Fy = 2500; // kg/cm²
            sg.E = 1.7E8; // kN/m²
            sg.Fat = 1900; // kg/cm²
            sg.Fac = 2200; // kg/cm²
            sg.Fas = 1300; // kg/cm²
        }

        double beta;

        double Facebeff;
        double FaceAeff;
        double Faceybar;
        double Faceinertia;

        double eta = 0.83; // η
        double zeta = 0.3; // ζ

        double Ks;
        double delta; // δ
        double Pv;
        double Ph1;
        double Ph2;
        double Pv4;
        double Pv5;
        double g;

        double jackP;
        int jackNum;

        double poreLatSpacing;
        double poreVerSpacing;
        int poreNum;
        public void Process()
        {            
            oSTN_VerticalStress.VerticalStress("TUNNEL", out string lt, out string st, out string surch, out double lE1,
                out double sE1, out double pt, out double lph1, out double lph2, out double sph1, out double sph2, out double uu);
            

            Props();
            delta = 0.007416;
            Pv = 344.52;
            Ph1 = 230.24;
            Ph2 = 316.66;
            Pv4 = 120.48;
            Calculation();

            // beta 查表參數
            SGCheck();

            jackP = 150; // t
            jackNum = 22;
            SGPush();

            poreLatSpacing = 0.25; // m
            poreVerSpacing = 0.075; // m
            poreNum = 4;
            SGPore();
        }

        #region Props
        public void Props()
        {
            Facebeff = 25 * sg.t1;
            double B = sg.width;
            if (Facebeff > B) Facebeff = B;

            FaceAeff = Facebeff * sg.t1 + (sg.t - sg.t1) * sg.t2 * 2;
            Faceybar = (Facebeff * sg.t1 * (sg.t - (sg.t1 / 2)) + (sg.t - sg.t1) * sg.t2 * ((sg.t - sg.t1) / 2) * 2) / FaceAeff;
            Faceinertia = Facebeff * Math.Pow(sg.t1, 3) / 12 +
                Facebeff * sg.t1 * Math.Pow((sg.t - (sg.t1 / 2) - Faceybar), 2) +
                sg.t2 * Math.Pow(sg.t - sg.t1, 3) / 12 * 2 + 
                sg.t2 * (sg.t - sg.t1) * Math.Pow((Faceybar - (sg.t - sg.t1) / 2), 2) * 2; // cm⁴

            Pv = oSTN_VerticalStress.PvTop; //kN/m²
            Ph1 = oSTN_VerticalStress.LongTermPh1; //kN/m²
            Ph2 = oSTN_VerticalStress.LongTermPh2; //kN/m²

            Ks = oSTN_VerticalStress.longTermSoilE * (1 - oSTN_VerticalStress.Nu12)
                / (sg.Rout * (1 + oSTN_VerticalStress.Nu12)) / (1 - 2 * oSTN_VerticalStress.Nu12); //kN/m³            

            Pv5 = 12.85; //kN/m²
            g = Pv5 / Math.PI;
            delta = (2 * Pv - Ph1 - Ph2 + Pv5) * Math.Pow(sg.Rout, 4)
                / (24 * (eta * sg.E * Faceinertia * 1E-8 + 0.0454 * Ks * Math.Pow(sg.Rout, 4))); // m 
            Pv4 = Ks * delta; //kN/m²
        }
        #endregion

        #region Calculation
        double M1 = 0;
        double M2 = 0;
        double Mmax;
        double Mcmax;
        double Mjmax;
        double Q1 = 0;
        double Q2 = 0;
        double Qmax;
        double Mcmin;
        double Mjmin;
        double P1;
        double P2;
        List<Tuple<int, double, double, double>> totalCal = new List<Tuple<int, double, double, double>>();
        public void Calculation()
        {
            List<Tuple<int, double, double, double>> PvCal = new List<Tuple<int, double, double, double>>();
            List<Tuple<int, double, double, double>> Ph1Cal = new List<Tuple<int, double, double, double>>();
            List<Tuple<int, double, double, double>> Ph2_1Cal = new List<Tuple<int, double, double, double>>();
            List<Tuple<int, double, double, double>> kdeltaCal = new List<Tuple<int, double, double, double>>();
            List<Tuple<int, double, double, double>> Pv5Cal = new List<Tuple<int, double, double, double>>();            

            int j = 2;
            for (int i = 0; i <= 180 ; i += 5)
            {
                double radi = i * Math.PI / 180;
                double sinI = Math.Sin(radi);
                double cosI = Math.Cos(radi);

                double MTotal = 0;
                double NTotal = 0;
                double QTotal = 0;

                double M = (1 - 2 * Math.Pow(sinI, 2)) / 4 * Pv * sg.Rout * sg.Rout;
                double N = Pv * sg.Rout * Math.Pow(sinI, 2);
                double Q = Pv * sg.Rout * sinI * cosI * (-1);
                PvCal.Add(Tuple.Create(i, M, N, Q));
                MTotal += M;
                NTotal += N;
                QTotal += Q;

                M = ((1 - 2 * Math.Pow(cosI, 2)) / 4) * Ph1 * sg.Rout * sg.Rout;
                N = Ph1 * sg.Rout * Math.Pow(cosI, 2);
                Q = Ph1 * sg.Rout * sinI * cosI;
                Ph1Cal.Add(Tuple.Create(i, M, N, Q));
                MTotal += M;
                NTotal += N;
                QTotal += Q;

                M = (6 - 3 * cosI - 12 * Math.Pow(cosI, 2) + 4 * Math.Pow(cosI, 3)) / 48 * (Ph2 - Ph1) * sg.Rout * sg.Rout;
                N = (cosI + 8 * Math.Pow(cosI, 2) - 4 * Math.Pow(cosI, 3)) / 16 * (Ph2 - Ph1) * sg.Rout;
                Q = (sinI + 8 * sinI * cosI - 4 * sinI * Math.Pow(cosI, 2)) / 16 * (Ph2 - Ph1) * sg.Rout;
                Ph2_1Cal.Add(Tuple.Create(i, M, N, Q));
                MTotal += M;
                NTotal += N;
                QTotal += Q;

                if (i <= 90)
                {
                    switch (i)
                    {
                        case int a when (a < 45):
                            M = (0.2346 - 0.3536 * cosI) * Ks * delta * sg.Rout * sg.Rout;
                            N = 0.3536 * cosI * Ks * delta * sg.Rout;
                            Q = 0.3536 * sinI * Ks * delta * sg.Rout;                            
                            break;
                        case int a when (a <= 90):
                            M = (-0.3487 + 0.5 * Math.Pow(sinI, 2) + 0.2357 * Math.Pow(cosI, 3)) * Ks * delta * sg.Rout * sg.Rout;
                            N = (-0.7071 * cosI + Math.Pow(cosI, 2) + 0.7071 * Math.Pow(sinI, 2) * cosI) * Ks * delta * sg.Rout;
                            Q = (sinI * cosI - 0.7071 * Math.Pow(cosI, 2) * sinI) * Ks * delta * sg.Rout;                            
                            break;
                    }                                        
                }
                else
                {
                    if(i == 180)
                    {
                        M = kdeltaCal[0].Item2;
                        N = kdeltaCal[0].Item3;
                        Q = kdeltaCal[0].Item4;
                    }
                    else
                    {
                        M = kdeltaCal[kdeltaCal.Count - j].Item2;
                        N = kdeltaCal[kdeltaCal.Count - j].Item3;
                        Q = kdeltaCal[kdeltaCal.Count - j].Item4;
                        j += 2;
                    }                    
                }
                kdeltaCal.Add(Tuple.Create(i, M, N, Q));
                MTotal += M;
                NTotal += N;
                QTotal += Q;

                switch (i)
                {
                    case int a when (a <= 90):
                        M = (Math.PI * 3 / 8 - radi * sinI - cosI * 5 / 6) * g * sg.Rout * sg.Rout;
                        N = (radi * sinI - cosI / 6) * g * sg.Rout;
                        Q = (radi * cosI + sinI / 6) * g * sg.Rout;
                        break;
                    default:
                        M = (Math.PI * (-1) / 8 + (Math.PI - radi) * sinI - cosI * 5 / 6 - Math.PI / 2 * Math.Pow(sinI, 2))
                            * g * Math.Pow(sg.Rout, 2);
                        N = (Math.PI * (-1) * sinI + radi * sinI + Math.PI * Math.Pow(sinI, 2) - cosI / 6) * g * sg.Rout;
                        Q = ((Math.PI - radi) * cosI - Math.PI * sinI * cosI - sinI / 6) * g * sg.Rout;
                        break;
                }
                Pv5Cal.Add(Tuple.Create(i, M, N, Q));
                MTotal += M;
                NTotal += N;
                QTotal += Q;

                totalCal.Add(Tuple.Create(i, MTotal, NTotal, QTotal));

                if (MTotal > M1) { M1 = MTotal; Mmax = MTotal; P1 = NTotal; } // kN-m
                if (MTotal < M2) { M2 = MTotal; P2 = NTotal; }// kN-m

                if (QTotal > Q1) { Q1 = QTotal; Qmax = QTotal; P2 = NTotal; } // kN
                if (QTotal < Q2) { Q2 = QTotal; P2 = NTotal; }// kN
            }

            Mcmax = (1 + zeta) * Mmax / p.newton; // t-m
            Mjmax = (1 - zeta) * Mmax / p.newton; // t-m
            Qmax /= p.newton; // t

            Mcmin = (1 + zeta) * M2 / p.newton; // t-m
            Mjmin = (1 - zeta) * M2 / p.newton; // t-m

            P1 /= p.newton; // t
            P2 /= p.newton; // t
        }
        #endregion

        
        public void SGCheck()
        {           
            // 鑄鐵環片外側板之檢測
            double stressP1t = P1 * 1E3 / FaceAeff - Mcmax * 1E5 * Faceybar / Faceinertia;
            bool P1tBool;
            if (stressP1t < sg.Fat) P1tBool = true;
            else P1tBool = false;

            double stressP1c = P1 * 1E3 / FaceAeff + Mcmax * 1E5 * (sg.t - Faceybar) / Faceinertia;
            bool P1cBool;
            if (stressP1c < sg.Fac) P1cBool = true;
            else P1cBool = false;

            double stressP2t = P2 * 1E3 / FaceAeff + Mcmin * 1E5 * (sg.t - Faceybar) / Faceinertia;
            bool P2tBool;
            if (stressP2t < sg.Fat) P2tBool = true;
            else P2tBool = false;

            double stressP2c = P2 * 1E3 / FaceAeff - Mcmin * 1E5 * Faceybar / Faceinertia;
            bool P2cBool;
            if (stressP2c < sg.Fac) P2cBool = true;
            else P2cBool = false;

            double lx;
            double ly;
            // 四邊固定支承矩形板彈性設計法
            ly = sg.width / 100; // m
            lx = sg.Rout * 2 * Math.PI * sg.theta / 360; // m

            double C = ly / lx;
            double Vx = 1 - (C * C * 5 / 18 / (1 + Math.Pow(C,4)));
            double Vy = Vx;
            double Wx = Pv / (Math.Pow(1 / C, 4) + 1);
            double Wy = Pv / (1 + Math.Pow(C, 4));

            double CentermaxMx = Vx / 24 * Wx * lx * lx;
            double CentermaxMy = Vy / 24 * Wy * ly * ly;

            double EdgeMaxMx = Wx * lx * lx / 12 * (-1);
            double EdgeMaxMy = Wy * ly * ly / 12 * (-1);

            double stressMx = (EdgeMaxMx / p.newton * 1E3 * 1E2 * (sg.t1 / 2)) / (lx * 100 * Math.Pow(sg.t1, 3) / 12);
            double stressMy = (EdgeMaxMy / p.newton * 1E3 * 1E2 * (sg.t1 / 2)) / (ly * 100 * Math.Pow(sg.t1, 3) / 12);

            bool MxElasticBool;
            bool MyElasticBool;
            if (stressMx < sg.Fat) MxElasticBool = true;
            if (stressMy < sg.Fat) MyElasticBool = true;

            // Seely 法
            double alpha = lx / ly;

            beta = Math.Round(0.0512 * Math.Pow(alpha, 3) - 0.0959 * Math.Pow(alpha, 2) + 0.0136 * alpha + 0.0632,2);
            //查圖得知，以用找點公式自動抓取

            double SeelyM = beta * Pv * 2 * lx * lx; // kN-m/m
            double SeelyStress = 6 * SeelyM * 100 / sg.t1 / sg.t1; // kg/cm²

            bool SeelyBool;
            if (SeelyStress < sg.Fat) SeelyBool = true;
            else SeelyBool = false;
        }

        public void SGPush()
        {
            double b1 = sg.t - sg.t1;
            double b2 = b1;
            double b3 = 0;

            double beff = 20 * sg.t1;
            double tempB = sg.theta / 360 * Math.PI * sg.Rout * 2 * 100;
            if (beff > tempB) beff = tempB;

            double LatAeff = beff * sg.t1 + (b2 + b3) * sg.t3;
            double Latybar = ((beff * sg.t1) * (sg.t - sg.t1 / 2) + (b2 * sg.t3) * (b2 / 2) + (b3 * sg.t3) * (sg.t3 / 2)) / LatAeff;
            double Latinertia = (beff * Math.Pow(sg.t1, 3) / 12) + beff * sg.t1 * Math.Pow(sg.t - sg.t1 / 2 - Latybar, 2) +
                sg.t3 * Math.Pow(b2, 3) / 12 + sg.t3 * b2 * Math.Pow(Latybar - b2 / 2, 2) +
                b3 * Math.Pow(sg.t3, 3) / 12 + b3 * sg.t3 * Math.Pow(Latybar - sg.t3 / 2, 2);
            double LatR = Math.Pow(Latinertia / LatAeff, 0.5);
            double slRatio = 1 * sg.width / LatR;

            double e = Latybar - (sg.t - sg.t1 / 2) / 2; // cm

            double eachP = jackP * jackNum * sg.theta / 360; // t

            double eachM = eachP * e * 1000; // kg-cm 
            double fa = eachP / LatAeff * 1000; // kg-cm
            double fb = eachM * Latybar / Latinertia; // kg-cm

            double fe = 12 * Math.PI * Math.PI * sg.E / 100 / (23 * slRatio * slRatio);

            double Cm = 0.85;

            double SFCM = fa / sg.Fac + Cm * fb / ((1 - fa / fe) * sg.Fat);

            double SF = fa / sg.Fac + fb / sg.Fat;
        }

        public void SGPore()
        {


            List<Tuple<double, double, double, double, double, double, bool>> poreCheck =
                new List<Tuple<double, double, double, double, double, double, bool>>();

            for(int i = 0; i < totalCal.Count; i++)
            {
                double m = totalCal[i].Item2 * 1.3 / p.newton / 2;
                double q = totalCal[i].Item4 * 1.3 / p.newton / 2;
                double V1 = q / poreNum;
                double V2 = m * poreVerSpacing / 2 / (4 * Math.Pow(poreVerSpacing / 2, 2) + 4 * Math.Pow(poreLatSpacing / 2, 2));
                double Vmax = Math.Pow(V1 * V1 + V2 * V2, 0.5);
                double Fv = 5.2;
                bool vCehck;
                if (Vmax < Fv) vCehck = true;
                else vCehck = false;

                poreCheck.Add(Tuple.Create(m, q, V1, V2, Vmax, Fv, vCehck));

            }
        }
    }
}