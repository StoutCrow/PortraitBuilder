using System;
using System.Collections.Generic;
using System.Text;

namespace PortraitBuilder.Model.Portrait
{
    public static class DefaultCharacteristics
    {
        public static Characteristic BACKGROUND = new("Background", 0, CharacteristicType.Property, true);
        public static Characteristic HAIR = new("Hair", 1, CharacteristicType.Property, true);
        public static Characteristic HEAD = new("Head", 2, CharacteristicType.Property, true);
        public static Characteristic CLOTHES = new("Clothes", 3, CharacteristicType.Property, true);
        public static Characteristic BEARD = new("Beard", 4, CharacteristicType.Property, true);
        public static Characteristic HEADGEAR = new("Headgear", 5, CharacteristicType.Property, true);
        public static Characteristic IMPRISONED = new("Imprisoned", 6, CharacteristicType.Property, false);
        public static Characteristic SCARS = new("Scars", 7, CharacteristicType.Property, false);
        public static Characteristic RED_DOTS = new("Reddots", 8, CharacteristicType.Property, false);
        public static Characteristic BOILS = new("Boils", 9, CharacteristicType.Property, false);
        public static Characteristic BLINDED = new("Blinded", 10, CharacteristicType.Property, false);
        public static Characteristic PLAYER = new("Player", 11, CharacteristicType.Property, false);
        public static Characteristic MASK = new("Mask", 12, CharacteristicType.Property, false);
        public static Characteristic EYEPATCH = new("Eyepatch", 13, CharacteristicType.Property, false);
        public static Characteristic MAKEUP = new("Makeup", 14, CharacteristicType.Property, false);
        public static Characteristic MAKEUP_2 = new("Makeup2", 15, CharacteristicType.Property, false);
        public static Characteristic JEWELRY = new("Jewelry", 16, CharacteristicType.Property, false);
        public static Characteristic IMMORTALITY = new("Immortality", 17, CharacteristicType.Property, false);
        public static Characteristic SPECIAL_CROWN_BEHIND = new("Crown behind", 18, CharacteristicType.Property, false);
        public static Characteristic SPECIAL_CROWN = new("Crown", 19, CharacteristicType.Property, false);
        public static Characteristic FRECKLES = new("Freckles", 20, CharacteristicType.Property, false);
        public static Characteristic PHYSIQUE = new("Physique", 21, CharacteristicType.Property, false);
        public static Characteristic PALE = new("Pale", 22, CharacteristicType.Property, false);
        public static Characteristic BLACK_EYE = new("Black eye", 23, CharacteristicType.Property, false);
        public static Characteristic HAIRELIP = new("Hairelip", 24, CharacteristicType.Property, false);
        public static Characteristic SCARS_MID = new("Scars mid", 25, CharacteristicType.Property, false);
        public static Characteristic SCARS_HIGH = new("Scars high", 26, CharacteristicType.Property, false);
        public static Characteristic BLOOD = new("Blood", 27, CharacteristicType.Property, false);
        public static Characteristic TATTOO = new("Tattoo", 28, CharacteristicType.Property, false);
        public static Characteristic WARPAINT = new("Warpaint", 29, CharacteristicType.Property, false);
        public static Characteristic POSSESSED = new("Possessed", 30, CharacteristicType.Property, false);
        public static Characteristic OVERLAYER_BEHIND = new("Overlayer behind", 31, CharacteristicType.Property, false);
        public static Characteristic OVERLAYER = new("Overlayer", 32, CharacteristicType.Property, false);
        public static Characteristic UNDERMAIN = new("Undermain", 33, CharacteristicType.Property, false);
        public static Characteristic SPECIAL_HELMET = new("Helmet", 34, CharacteristicType.Property, false);
        public static Characteristic SPECIAL_MASK = new("Mask", 35, CharacteristicType.Property, false);
        public static Characteristic SPECIAL_SCEPTER = new("Scepter", 36, CharacteristicType.Property, false);
        public static Characteristic RELATIONSHIP = new("Relationship", 37, CharacteristicType.Property, false);

        public static Characteristic NECK = new("Neck", 0, CharacteristicType.DNA, true);
        public static Characteristic CHIN = new("Chin", 1, CharacteristicType.DNA, true);
        public static Characteristic MOUTH = new("Mouth", 2, CharacteristicType.DNA, true);
        public static Characteristic NOSE = new("Nose", 3, CharacteristicType.DNA, true);
        public static Characteristic CHEEKS = new("Cheeks", 4, CharacteristicType.DNA, true);
        public static Characteristic D5 = new("Unused", 5, CharacteristicType.DNA, true);
        public static Characteristic EYES = new("Eyes", 6, CharacteristicType.DNA, true);
        public static Characteristic EARS = new("Ears", 7, CharacteristicType.DNA, true);
        public static Characteristic HAIR_COLOR = new("Haircolor", 8, CharacteristicType.DNA, true);
        public static Characteristic EYE_COLOR = new("Eyecolor", 9, CharacteristicType.DNA, true);
        public static Characteristic D10 = new("Unused", 10, CharacteristicType.DNA, true);

        public static Characteristic[] DNA = new Characteristic[] { NECK, CHIN, MOUTH, NOSE, CHEEKS, D5, EYES, EARS, HAIR_COLOR, EYE_COLOR, D10 };
        public static Characteristic[] PROPERTIES = new Characteristic[] { BACKGROUND, HAIR, HEAD, CLOTHES, BEARD, HEADGEAR, IMPRISONED, SCARS, RED_DOTS, BOILS,
            BLINDED, PLAYER, MASK, EYEPATCH, MAKEUP, MAKEUP_2, JEWELRY, IMMORTALITY, SPECIAL_CROWN_BEHIND, SPECIAL_CROWN, FRECKLES, PHYSIQUE, PALE, BLACK_EYE,
            HAIRELIP, SCARS_MID, SCARS_HIGH, BLOOD, TATTOO, WARPAINT, POSSESSED, OVERLAYER_BEHIND, OVERLAYER, UNDERMAIN,
            SPECIAL_HELMET, SPECIAL_MASK, SPECIAL_SCEPTER, RELATIONSHIP };


        public static Characteristic GetProperty(int index)
        {
            if (index < PROPERTIES.Length)
            {
                return PROPERTIES[index];
            }
            else
            {
                // As defines are not parsed, always consider custom properties as valid.
                return new Characteristic("Custom", index, CharacteristicType.Property, custom: true);
            }
        }

        public static Characteristic GetDNA(int index)
        {
            try
            {
                return DNA[index];
            }
            catch (IndexOutOfRangeException)
            {
                throw new IndexOutOfRangeException($"Characteristic d{index} does not exist.");
            }
        }

    }
}
