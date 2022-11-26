import React, {Fragment, FunctionComponent, ReactElement, useEffect} from "react";
import {Accordion, Button, Card, Col, FormControl, FormGroup, FormLabel, Row} from "react-bootstrap";
import {Character, Pool} from "../char";
import {PointsControl} from "../PointsControl";
import {PointsPreviewData} from "../drag";
import cloneDeep from "lodash.clonedeep";
import {UndoManager} from "../undo";
import {MdContentCopy, MdDeleteForever} from "react-icons/md";
import {v4} from "uuid";
import {useDeferredFn, usePromiseFn} from "../asyncData";

export type ICharacterControlProps = {
  characterId: string;
  undoManager: UndoManager;
  editMode: boolean;
};

function copyWith<T>(update: (value: T) => void): (value: T) => T {
  return (value: T) => {
    const copy = cloneDeep(value);
    update(copy);
    return copy;
  }
}

function useCharacter(
  undoManager: UndoManager,
  characterId: string
): [Character | null, React.Dispatch<React.SetStateAction<Character | null>>] {
  const { data: char, reload: reloadChar } = usePromiseFn(
    async () => (await undoManager.characterPersistence.load())[characterId],
    [characterId]
  );

  useEffect(() => {
    const stateChanged = () => {
      if (char) {
        reloadChar();
      }
    };
    undoManager.on("change", stateChanged);
    return () => {
      undoManager.off("change", stateChanged);
    };
  }, [char]);

  return [char ?? null, async (newValue) => {
    const effectiveValue = newValue instanceof Function ? newValue(char ?? null) : newValue;
    if (effectiveValue) {
      await undoManager.updateCharacter(effectiveValue);
    }
  }];
}

const capacity: Record<"lp" | "fp", {
  lineCapacity: (baseCapacity: number) => number,
  totalCapacity: (baseCapacity: number) => number
}> = {
  lp: {
    lineCapacity: (baseCapacity: number) => baseCapacity,
    totalCapacity: (baseCapacity: number) => baseCapacity * 5
  },
  fp: {
    lineCapacity: (_) => 12,
    totalCapacity: (baseCapacity: number) => baseCapacity
  }
};

export const CharacterControl: FunctionComponent<ICharacterControlProps> = ({
  characterId,
  undoManager,
  editMode
}) => {
  const [char, setChar] = useCharacter(undoManager, characterId);

  function applyPointsReceived(poolOf: (char: Character) => Pool, data: PointsPreviewData): void {
    const receivedPoints = data.points;
    setChar(copyWith(char => {
      if (!char) {
        return null;
      }
      const pool = poolOf(char);
      const currentPoints = pool.points;
      pool.points = {
        exhausted: Math.max(0, currentPoints.exhausted + receivedPoints.exhausted),
        consumed: Math.max(0, currentPoints.consumed + receivedPoints.consumed),
        channeled: Math.max(0, currentPoints.channeled + receivedPoints.channeled)
      };

      if (receivedPoints.channeled > 0) {
        console.log("remember channeling of ", receivedPoints.channeled);
        if(!pool.channellings) {
          pool.channellings = [];
        }
        pool.channellings.push(receivedPoints.channeled);
      }

      if (data.channelingIndex != null) {
        console.log("remove channeling index ", data.channelingIndex);
        pool.channellings = (pool.channellings ?? []).filter((c,i) => i != data.channelingIndex);
      }
    }));
  }

  function pointsControl(
    poolOf: (char: Character) => Pool,
    title: string,
    baseCapacityLabel: string,
    showPenalties: boolean,
    focus: "lp" | "fp"
  ): ReactElement | null {
    if (!char) {
      return null;
    }
    const pool = poolOf(char);
    return <PointsControl eventKey={focus} points={pool.points} baseCapacity={pool.baseCapacity}
                          channellings={pool.channellings ?? []}
                          title={title}
                          showPenalties={showPenalties}
                          {...capacity[focus]}
                          onReceivePoints={(points) => applyPointsReceived(poolOf, points)}
    />;
  }

  function changeName(newName: string) {
    if (!char) {
      return;
    }
    if (newName !== char.name) {
      setChar(copyWith(char => {
        if (char) {
          char.name = newName;
        }
      }));
    }
  }

  const cloneCharacter = useDeferredFn(async () => {
    if (!char) {
      return;
    }
    const newChar = cloneDeep(char);
    (newChar as unknown as any)["id"] = v4();
    newChar.name = `${newChar.name} Klon`;
    await undoManager.addCharacter(newChar);
  }, [undoManager, char]);

  const deleteCharacter = useDeferredFn(async () => {
    if (!char) {
      return;
    }
    await undoManager.removeCharacter(characterId);
  }, [undoManager, characterId]);

  function changeBaseCapacity(newBaseCapacity: number, poolOf: (char: Character) => Pool) {
    if(!char){
      return;
    }
    if(!Number.isNaN(newBaseCapacity)) {
      setChar(copyWith(char => {
        if (!char) {
          return;
        }
        poolOf(char).baseCapacity = newBaseCapacity;
      }));
    }
  }

  if(!char) {
    return null;
  }

  const editRowClasses = "g-5 px-4 py-2";

  return <Fragment>
    {!editMode && <Fragment>
      <Row as={Card.Title} className="gx-0 py-2">
        <Col as="h2">{char?.name}</Col>
      </Row>
      <Row as={Accordion} flush className="gx-0">
        {pointsControl(c => c.lp, "Lebenspunkte 💖", "LP", true, "lp")}
        {pointsControl(c => c.fo, "Fokuspunkte ✨", "FP", false, "fp")}
      </Row>
    </Fragment>}
    {editMode && <Fragment>
      <Row className={editRowClasses}>
        <FormControl type="text" size="lg" className="col" value={char?.name} onChange={e => changeName(e.target.value)}
                     maxLength={100}/>
      </Row>
      <Row className={editRowClasses}>
        <Col xs={"auto"}>

        </Col>
      </Row>
      <FormGroup as={Row} className={editRowClasses}>
        <FormLabel column xs={3} md={2}>
          LP
        </FormLabel>
        <Col xs={6} md={8} className={"gx-3"}>
          <FormControl type="number" value={char.lp.baseCapacity} min={1} max={20} onChange={e => changeBaseCapacity(parseInt(e.target.value), c => c.lp)} />
        </Col>
        <Col xs={3} md={2} className={"gx-4"}>
          <FormControl type="plaintext"  plaintext readOnly value={"= " + capacity.lp.totalCapacity(char.lp.baseCapacity)} />
        </Col>
      </FormGroup>
      <FormGroup as={Row} className={editRowClasses}>
        <FormLabel column xs={3} md={2}>
          FO
        </FormLabel>
        <Col xs={6} md={8} className={"gx-3"}>
          <FormControl type="number" value={char.fo.baseCapacity} min={1} max={12*5} onChange={e => changeBaseCapacity(parseInt(e.target.value), c => c.fo)} />
        </Col>
      </FormGroup>
      <Row className={editRowClasses}>
        <Button variant="primary" className="col" onClick={cloneCharacter}>
          Klonen <MdContentCopy/>
        </Button>
      </Row>
      <Row className={editRowClasses}>
        <Button variant="danger" className="col" onClick={deleteCharacter}>
          Löschen <MdDeleteForever/>
        </Button>
      </Row>
    </Fragment>}
  </Fragment>
};