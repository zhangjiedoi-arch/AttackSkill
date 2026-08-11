namespace AttackSkill.TestDemo
{
    public class Test
    {
        char[] ReplaceString(char[] chars, char[] newcchars, char[] oldcchars)
        {
            if (chars == null || newcchars == null || oldcchars == null || chars.Length == 0 || newcchars.Length == 0 || oldcchars.Length == 0)
            {
                return chars;
            }

            int[] indexs = new int[chars.Length];
            int index_index = 0;
            bool isReplace = false;
            for (int i = 0; i < chars.Length; i++)
            {
                isReplace = true;
                for (int j = 0; j < oldcchars.Length; j++)
                {
                    if (chars[i + j] != oldcchars[j])
                    {
                        isReplace = false;
                        break;
                    }
                }
                if (isReplace)
                {
                    indexs[index_index] = i;
                    index_index++;
                    i += oldcchars.Length - 1;
                }
            }
            if (index_index == 0) return chars;

            int newchars_length = chars.Length - index_index * oldcchars.Length + index_index * newcchars.Length;
            char[] newchars = new char[newchars_length];
            index_index = 0;
            int newchars_index = 0;
            for (int i = 0; i < newchars_length; i++)
            {
                if (i < indexs[index_index] || (index_index > 0 && indexs[index_index] == 0))
                {
                    newchars[newchars_index] = chars[i];
                    newchars_index++;
                }
                else
                {
                    for (int j = 0; j < newcchars.Length; j++)
                    {
                        newchars[newchars_index + j] = newcchars[j];
                    }
                    if (i + oldcchars.Length > chars.Length) break;
                    index_index++;
                    i += oldcchars.Length - 1;
                    newchars_index += newcchars.Length;
                }
            }

            return newchars;
        }

    }
}