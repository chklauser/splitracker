using Raven.Migrations;

namespace Splitracker.Persistence.Characters.DataMigrations;

[Migration(1)]
class ChannelingsHaveDescription : Migration
{
    public override void Up()
    {
        PatchCollection("""
            from "Characters" as c
            update {
                c.Lp.Channelings = 
                    c.Lp.Channelings.map((ch,idx) => 
                        ({ Value: ch, Description: null, Id: idx.toString() })
                    )
                c.Fo.Channelings = 
                    c.Fo.Channelings.map((ch,idx) => 
                        ({ Value: ch, Description: null, Id: idx.toString() })
                    )
            }
            """);
    }

    public override void Down()
    {
        PatchCollection("""
            from "Characters" as c
            update {
                c.Lp.Channelings = 
                    c.Lp.Channelings.map(ch => ch.Value)
                c.Fo.Channelings = 
                    c.Fo.Channelings.map(ch => ch.Value)
            }
            """);
    }
}