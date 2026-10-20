const UUID_BYTE_COUNT = 16;
const BYTE_VALUE_COUNT = 256;
const UUID_VERSION_BYTE_INDEX = 6;
const UUID_VERSION_VALUE_MASK = 0x0f;
const UUID_VERSION_FOUR_BITS = 0x40;
const UUID_VARIANT_BYTE_INDEX = 8;
const UUID_VARIANT_VALUE_MASK = 0x3f;
const UUID_RFC_VARIANT_BITS = 0x80;
const HEX_RADIX = 16;
const HEX_BYTE_WIDTH = 2;
const HEX_PADDING_CHARACTER = '0';
const UUID_TIME_LOW_END = 8;
const UUID_TIME_MID_END = 12;
const UUID_TIME_HIGH_END = 16;
const UUID_CLOCK_SEQUENCE_END = 20;
const UUID_GROUP_SEPARATOR = '-';

/**
 * Returns a RFC 4122 version 4 UUID in browsers with or without
 * crypto.randomUUID(). Browser-generated IDs are editor identities only; they
 * are not used as secrets or authorization tokens.
 */
export function createUuid(): string {
  const cryptoApi = globalThis.crypto;
  if (typeof cryptoApi?.randomUUID === 'function') {
    const randomUUIDResult: string = cryptoApi.randomUUID();
    return randomUUIDResult;
  }
  const bytes = new Uint8Array(UUID_BYTE_COUNT);
  if (typeof cryptoApi?.getRandomValues === 'function') {
    cryptoApi.getRandomValues(bytes);
  }
  else {
    for (let index = 0; index < bytes.length; index += 1) {
      const randomByteValue: number = Math.random() * BYTE_VALUE_COUNT;
      bytes[index] = Math.floor(randomByteValue);
    }
  }
  bytes[UUID_VERSION_BYTE_INDEX] = (bytes[UUID_VERSION_BYTE_INDEX]! & UUID_VERSION_VALUE_MASK) | UUID_VERSION_FOUR_BITS;
  bytes[UUID_VARIANT_BYTE_INDEX] = (bytes[UUID_VARIANT_BYTE_INDEX]! & UUID_VARIANT_VALUE_MASK) | UUID_RFC_VARIANT_BITS;
  const fromResult = Array.from(bytes, (value) => {
    const toStringResult = value.toString(HEX_RADIX);
    const padStartResult: string = toStringResult.padStart(HEX_BYTE_WIDTH, HEX_PADDING_CHARACTER);
    return padStartResult;
  });
  const hex = fromResult.join('');
  const timeLow = hex.slice(0, UUID_TIME_LOW_END);
  const timeMid = hex.slice(UUID_TIME_LOW_END, UUID_TIME_MID_END);
  const timeHigh = hex.slice(UUID_TIME_MID_END, UUID_TIME_HIGH_END);
  const clockSequence = hex.slice(UUID_TIME_HIGH_END, UUID_CLOCK_SEQUENCE_END);
  const nodeIdentifier = hex.slice(UUID_CLOCK_SEQUENCE_END);
  const uuid = `${timeLow}${UUID_GROUP_SEPARATOR}${timeMid}${UUID_GROUP_SEPARATOR}${timeHigh}${UUID_GROUP_SEPARATOR}${clockSequence}${UUID_GROUP_SEPARATOR}${nodeIdentifier}`;
  return uuid;
}
